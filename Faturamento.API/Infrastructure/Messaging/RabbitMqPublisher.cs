using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Faturamento.API.Infrastructure.Messaging;

/// <summary>
/// Publica eventos no RabbitMQ.
/// Faz health check próprio antes de publicar para distinguir
/// falha do RabbitMQ de falha do Estoque.API.
/// </summary>
public interface IRabbitMqPublisher
{
    void Publish<T>(string exchange, string routingKey, T message);
    bool IsAvailable();
}

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private bool _disposed;

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            Port     = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
            UserName = config["RabbitMQ:Username"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest",
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
            SocketReadTimeout          = TimeSpan.FromSeconds(5),
            SocketWriteTimeout         = TimeSpan.FromSeconds(5)
        };

        _connection = factory.CreateConnection("faturamento-publisher");
        _channel    = _connection.CreateModel();

        // Declara exchange e fila duráveis
        _channel.ExchangeDeclare("korp.faturamento", ExchangeType.Direct, durable: true, autoDelete: false);
        _channel.QueueDeclare("nota.impressa", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind("nota.impressa", "korp.faturamento", "nota.impressa");

        _logger.LogInformation("[RabbitMQ] Publisher conectado com sucesso.");
    }

    public bool IsAvailable() => _connection.IsOpen && _channel.IsOpen;

    public void Publish<T>(string exchange, string routingKey, T message)
    {
        if (!IsAvailable())
            throw new InvalidOperationException("RabbitMQ connection is not open.");

        var json  = JsonSerializer.Serialize(message);
        var body  = Encoding.UTF8.GetBytes(json);
        var props = _channel.CreateBasicProperties();
        props.Persistent   = true;   // mensagem sobrevive a restart do RabbitMQ
        props.ContentType  = "application/json";
        props.MessageId    = Guid.NewGuid().ToString();
        props.Timestamp    = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _channel.BasicPublish(exchange, routingKey, props, body);
        _logger.LogInformation("[RabbitMQ] Mensagem publicada: {Exchange}/{Key} | {Json}", exchange, routingKey, json);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _channel?.Close();
        _connection?.Close();
        _disposed = true;
    }
}
