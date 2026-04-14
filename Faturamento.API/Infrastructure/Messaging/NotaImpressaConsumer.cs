using System.Text;
using System.Text.Json;
using Faturamento.API.Domain.Interfaces;
using Faturamento.API.Application.Interfaces;
using Faturamento.API.Infrastructure.Data;
using Faturamento.API.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Faturamento.API.Infrastructure.Messaging;

/// <summary>
/// Consumer do RabbitMQ hospedado como BackgroundService.
/// Consome a fila "nota.impressa", chama o Estoque.API via HTTP,
/// e ao confirmar o abatimento, fecha a nota e notifica via SignalR.
/// </summary>
public class NotaImpressaConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<NotaImpressaConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public NotaImpressaConsumer(
        IServiceProvider services,
        IConfiguration config,
        ILogger<NotaImpressaConsumer> logger)
    {
        _services = services;
        _config   = config;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguarda RabbitMQ ficar disponível (pode demorar no docker compose up)
        await ConectarComRetryAsync(stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.Received += async (_, ea) =>
        {
            var body    = ea.Body.ToArray();
            var json    = Encoding.UTF8.GetString(body);
            var msgId   = ea.BasicProperties.MessageId ?? "(sem id)";

            _logger.LogInformation("[Consumer] Mensagem recebida: {MsgId} | {Json}", msgId, json);

            NotaImpressaEvent? evt = null;
            try
            {
                evt = JsonSerializer.Deserialize<NotaImpressaEvent>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Consumer] Falha ao desserializar mensagem {MsgId}.", msgId);
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: false);
                return;
            }

            if (evt is null)
            {
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: false);
                return;
            }

            bool sucesso = await ProcessarEventoAsync(evt, msgId);

            if (sucesso)
                _channel!.BasicAck(ea.DeliveryTag, false);
            else
                // Requeue: tenta novamente quando Estoque.API voltar
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: true);
        };

        _channel!.BasicConsume("nota.impressa", autoAck: false, consumer: consumer);
        _logger.LogInformation("[Consumer] Aguardando mensagens na fila 'nota.impressa'.");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task<bool> ProcessarEventoAsync(NotaImpressaEvent evt, string msgId)
    {
        using var scope = _services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<FaturamentoDbContext>();
        var estoque = scope.ServiceProvider.GetRequiredService<IEstoqueClient>();
        var hub     = scope.ServiceProvider.GetRequiredService<IHubContext<NotaFiscalHub>>();

        var nota = await db.NotasFiscais
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == evt.NotaFiscalId);

        if (nota is null)
        {
            _logger.LogWarning("[Consumer] Nota {Id} não encontrada — descartando.", evt.NotaFiscalId);
            return true; // ack — mensagem inválida, não reprocessar
        }

        if (nota.Status == Domain.Entities.StatusNota.Fechada)
        {
            _logger.LogWarning("[Consumer] Nota #{Numero} já está Fechada — idempotência, descartando.", nota.Numero);
            return true; // já foi processada antes, ack
        }

        try
        {
            // Abate estoque
            var itens = nota.Itens
                .Select(i => new ItemAbatimento(i.CodigoProduto, i.Quantidade))
                .ToList();

            await estoque.AbaterSaldoLoteAsync(itens);

            // Confirma fechamento da nota
            nota.ConfirmarImpressao();
            await db.SaveChangesAsync();

            _logger.LogInformation("[Consumer] Nota #{Numero} fechada com sucesso.", nota.Numero);

            await NotificarStatusSignalR(hub, nota, "Fechada", $"Nota #{nota.Numero:D6} processada com sucesso.");
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError("Falha de negócio para Nota #{Numero}: {Msg}", nota.Numero, ex.Message);

            await NotificarStatusSignalR(hub, nota, "Erro", $"Falha: {ex.Message}");

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "[Consumer] Estoque.API indisponível para Nota #{Numero}. Requeue.", nota.Numero);
            return false; // nack + requeue (tentará mais tarde)
        }
    }

    private async Task NotificarStatusSignalR(IHubContext<NotaFiscalHub> hub, Domain.Entities.NotaFiscal nota, string status, string mensagem)
    {
        await hub.Clients.All.SendAsync("NotaAtualizada", new
        {
            id = nota.Id,
            numero = nota.Numero,
            status = status,
            mensagem = mensagem
        });
    }

    private async Task ConectarComRetryAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName         = _config["RabbitMQ:Host"] ?? "localhost",
            Port             = int.Parse(_config["RabbitMQ:Port"] ?? "5672"),
            UserName         = _config["RabbitMQ:Username"] ?? "guest",
            Password         = _config["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = true
        };

        for (int attempt = 1; !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                _connection = factory.CreateConnection("faturamento-consumer");
                _channel    = _connection.CreateModel();
                _channel.ExchangeDeclare("korp.faturamento", ExchangeType.Direct, durable: true);
                _channel.QueueDeclare("nota.impressa", durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind("nota.impressa", "korp.faturamento", "nota.impressa");
                _channel.BasicQos(0, prefetchCount: 1, false); // processa 1 mensagem por vez
                _logger.LogInformation("[Consumer] Conectado ao RabbitMQ na tentativa {N}.", attempt);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Consumer] Tentativa {N}: {Msg}. Aguardando 5s...", attempt, ex.Message);
                await Task.Delay(5000, ct);
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}

public record NotaImpressaEvent(int NotaFiscalId, int Numero, List<ItemAbatimento> Itens, DateTime EmitidoEm);
public record ItemAbatimento(string CodigoProduto, decimal Quantidade);
