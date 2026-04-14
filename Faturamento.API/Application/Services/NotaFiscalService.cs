using Faturamento.API.Application.DTOs;
using Faturamento.API.Application.Interfaces;
using Faturamento.API.Domain.Entities;
using Faturamento.API.Domain.Interfaces;
using Faturamento.API.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.API.Application.Services;

public class NotaFiscalService : INotaFiscalService
{
    private readonly INotaFiscalRepository _repository;
    private readonly IRabbitMqPublisher _rabbit;
    private readonly IEstoqueClient _estoqueClient;
    private readonly ILogger<NotaFiscalService> _logger;

    public NotaFiscalService(
        INotaFiscalRepository repository,
        IRabbitMqPublisher rabbit,
        IEstoqueClient estoqueClient,
        ILogger<NotaFiscalService> logger)
    {
        _repository    = repository;
        _rabbit        = rabbit;
        _estoqueClient = estoqueClient;
        _logger        = logger;
    }

    public async Task<NotaFiscalResponse> CriarAsync(CriarNotaRequest request)
    {
        var itensRequest = request.Itens?.ToList()
            ?? throw new ArgumentException("Itens são obrigatórios.");

        if (!itensRequest.Any())
            throw new InvalidOperationException("A nota deve ter pelo menos um item.");

        var duplicados = itensRequest
            .GroupBy(i => i.CodigoProduto.ToUpper())
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        if (duplicados.Any())
            throw new InvalidOperationException($"Itens duplicados: {string.Join(", ", duplicados)}.");

        foreach (var item in itensRequest)
        {
            var produto = await _estoqueClient.ObterPorCodigoAsync(item.CodigoProduto);

            if (produto == null)
                throw new KeyNotFoundException($"Produto {item.CodigoProduto} não encontrado no estoque.");

            if (produto.Saldo < item.Quantidade)
            {
                throw new InvalidOperationException($"Saldo insuficiente para o produto {item.CodigoProduto}. Disponível: {produto.Saldo}, Solicitado: {item.Quantidade}");
            }
        }

        var itens = itensRequest.Select(i =>
            new ItemNota(i.CodigoProduto, i.DescricaoProduto, i.Quantidade, i.ValorUnitario)).ToList();

        var nota = new NotaFiscal(itens);
        await _repository.AddAsync(nota);
        _logger.LogInformation("Nota #{Numero} criada.", nota.Numero);
        return ToResponse(nota);
    }

    public async Task<NotaFiscalResponse?> ObterPorIdAsync(int id)
    {
        var nota = await _repository.GetByIdAsync(id);
        return nota is null ? null : ToResponse(nota);
    }

    public async Task<IEnumerable<NotaFiscalResponse>> ListarAsync()
    {
        var notas = await _repository.GetAllAsync();
        return notas.OrderByDescending(n => n.Numero).Select(ToResponse);
    }

    /// <summary>
    /// Fluxo de impressão com RabbitMQ + Graceful Degradation:
    ///
    /// 1. Verifica se RabbitMQ está disponível → se não, retorna 503 (falha crítica)
    /// 2. Tenta IniciarProcessamento() com RowVersion → protege contra race condition
    /// 3. Faz health check no Estoque.API
    ///    a. Estoque ONLINE  → publica na fila e muda status para Processando
    ///       O consumer abaterá o estoque e fechará a nota, notificando via SignalR
    ///    b. Estoque OFFLINE → publica na fila e muda status para Processando
    ///       Informa ao frontend que a nota será fechada quando o Estoque se recuperar
    /// </summary>
    public async Task<ImprimirResponse> ImprimirAsync(int notaFiscalId)
    {
        var nota = await _repository.GetByIdAsync(notaFiscalId)
            ?? throw new KeyNotFoundException($"Nota {notaFiscalId} não encontrada.");

        // Idempotência: só Aberta pode ser impressa
        if (nota.Status != StatusNota.Aberta)
            throw new InvalidOperationException(
                $"Apenas notas Abertas podem ser impressas. Status atual: {nota.Status}.");

        // Verifica RabbitMQ ANTES de mudar qualquer estado no banco
        if (!_rabbit.IsAvailable())
        {
            _logger.LogError("RabbitMQ indisponível. Nota #{Numero} não pode ser processada.", nota.Numero);
            throw new MensageriaIndisponivelException(
                "O sistema de mensageria interno está indisponível. A nota fiscal não pôde ser impressa " +
                "e seu status foi mantido como 'Aberta' por segurança. Tente novamente em alguns instantes.");
        }

        // Muda para Processando com RowVersion — se duas requisições chegarem simultaneamente,
        // a segunda receberá DbUpdateConcurrencyException (idempotência concorrente)
        try
        {
            nota.IniciarProcessamento();
            await _repository.UpdateAsync(nota);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Esta nota já está sendo processada por outra requisição simultânea. Aguarde a conclusão.");
        }

        // Health check rápido no Estoque.API (timeout: 3s)
        bool estoqueOnline = await _estoqueClient.IsHealthyAsync();

        // Publica evento na fila independente do estado do Estoque
        // O consumer tentará processar quando o Estoque estiver disponível
        var evento = new NotaImpressaEvent(
            NotaFiscalId: nota.Id,
            Numero:       nota.Numero,
            Itens:        nota.Itens.Select(i => new ItemAbatimento(i.CodigoProduto, i.Quantidade)).ToList(),
            EmitidoEm:    DateTime.UtcNow
        );

        _rabbit.Publish("korp.faturamento", "nota.impressa", evento);
        _logger.LogInformation("Nota #{Numero} publicada na fila. Estoque online: {Online}", nota.Numero, estoqueOnline);

        return new ImprimirResponse(
            Nota:           ToResponse(nota),
            EstoqueOnline:  estoqueOnline,
            Mensagem: estoqueOnline
                ? $"Nota #{nota.Numero:D6} enviada para processamento. O status será atualizado para Fechada em instantes."
                : $"Nota #{nota.Numero:D6} registrada com sucesso. O serviço de Estoque está com lentidão — " +
                  "o status será atualizado para Fechada automaticamente assim que o sistema se recuperar."
        );
    }

    public async Task<NotaFiscalResponse> CancelarAsync(int notaFiscalId)
    {
        var nota = await _repository.GetByIdAsync(notaFiscalId)
            ?? throw new KeyNotFoundException($"Nota {notaFiscalId} não encontrada.");
        nota.Cancelar();
        await _repository.UpdateAsync(nota);
        return ToResponse(nota);
    }

    private static NotaFiscalResponse ToResponse(NotaFiscal nota) => new(
        nota.Id, nota.Numero, nota.DataEmissao, nota.Status.ToString(), nota.ValorTotal,
        nota.Itens.Select(i => new ItemNotaResponse(
            i.Id, i.CodigoProduto, i.DescricaoProduto, i.Quantidade, i.ValorUnitario, i.Subtotal))
    );
}

public class MensageriaIndisponivelException : Exception
{
    public MensageriaIndisponivelException(string msg) : base(msg) { }
}
