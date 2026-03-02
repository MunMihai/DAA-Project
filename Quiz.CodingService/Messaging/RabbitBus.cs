using System.Text.Json;
using RabbitMQ.Client;

namespace Quiz.CodingService.Messaging;

/// <summary>
/// Lightweight RabbitMQ publisher for coding sessions.
/// Exchange: livecoding.events (topic, durable)
/// </summary>
public sealed class RabbitBus(IConfiguration cfg, ILogger<RabbitBus> log)
{
    private const string Exchange = "livecoding.events";

    private ConnectionFactory CreateFactory()
    {
        var factory = new ConnectionFactory { AutomaticRecoveryEnabled = true };
        var connStr = cfg.GetConnectionString("rabbit");

        if (!string.IsNullOrWhiteSpace(connStr))
        {
            factory.Uri = new Uri(connStr);
        }
        else
        {
            factory.HostName = cfg["Rabbit:Host"] ?? "localhost";
            factory.Port = int.TryParse(cfg["Rabbit:Port"], out var p) ? p : 5672;
            factory.UserName = cfg["Rabbit:User"] ?? "guest";
            factory.Password = cfg["Rabbit:Pass"] ?? "guest";
        }

        return factory;
    }

    public Task<IConnection> CreateConnectionAsync(CancellationToken ct = default) =>
        CreateFactory().CreateConnectionAsync(cancellationToken: ct);

    public Task EnsureTopologyAsync(IChannel ch, CancellationToken ct = default) =>
        ch.ExchangeDeclareAsync(
            exchange: Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

    public async Task PublishAsync<T>(
        string routingKey,
        T message,
        CancellationToken ct = default)
    {
        try
        {
            await using var conn = await CreateConnectionAsync(ct);
            await using var ch = await conn.CreateChannelAsync(cancellationToken: ct);
            await EnsureTopologyAsync(ch, ct);

            var body = JsonSerializer.SerializeToUtf8Bytes(
                message, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };

            await ch.BasicPublishAsync(
                exchange: Exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "RabbitMQ publish failed for key {RoutingKey}", routingKey);
        }
    }
}
