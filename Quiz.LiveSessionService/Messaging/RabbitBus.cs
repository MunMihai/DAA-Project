using System.Text.Json;
using RabbitMQ.Client;

namespace Quiz.LiveSessionService.Messaging;

/// <summary>
/// Lightweight RabbitMQ publisher.
/// Exchange: livequiz.events (topic, durable)
/// Routing keys: player.*, session.*, question.*, answer.*, score.*
///
/// Creates a connection per publish — simple and resilient for low-volume events.
/// For high-volume scenarios, consider a persistent channel pool.
/// </summary>
public sealed class RabbitBus(IConfiguration cfg, ILogger<RabbitBus> log)
{
    private const string Exchange = "livequiz.events";

    private ConnectionFactory CreateFactory() => new()
    {
        HostName = cfg["Rabbit:Host"] ?? "localhost",
        Port = int.TryParse(cfg["Rabbit:Port"], out var p) ? p : 5672,
        UserName = cfg["Rabbit:User"] ?? "guest",
        Password = cfg["Rabbit:Pass"] ?? "guest",
        AutomaticRecoveryEnabled = true
    };

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
            // RabbitMQ is best-effort for live events — don't fail the hub call
            log.LogWarning(ex, "RabbitMQ publish failed for key {RoutingKey}", routingKey);
        }
    }

    // Overload for hub compatibility
    public Task PublishAsync<T>(string routingKey, T message, IChannel ch, CancellationToken ct = default)
        => PublishAsync(routingKey, message, ct);
}