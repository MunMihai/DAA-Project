using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Quiz.CodingService.Hubs;

namespace Quiz.CodingService.Messaging;

public sealed class RabbitEventConsumer(
    RabbitBus bus,
    IHubContext<LiveCodingHub> hub,
    ILogger<RabbitEventConsumer> log
) : BackgroundService
{
    private static readonly string[] BindingKeys =
    [
        "player.*",
        "session.*",
        "score.*",
        "code.*"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsumerAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "RabbitMQ consumer crashed, retrying in 5s...");
                await Task.Delay(5_000, stoppingToken);
            }
        }
    }

    private async Task RunConsumerAsync(CancellationToken ct)
    {
        await using var conn = await bus.CreateConnectionAsync(ct);
        await using var ch = await conn.CreateChannelAsync(cancellationToken: ct);

        await bus.EnsureTopologyAsync(ch, ct);

        var q = await ch.QueueDeclareAsync(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: ct);

        foreach (var key in BindingKeys)
            await ch.QueueBindAsync(q.QueueName, "livecoding.events", key, cancellationToken: ct);

        await ch.BasicQosAsync(prefetchSize: 0, prefetchCount: 50, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(ch);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(ea.Body);
                var json = doc.RootElement.Clone();

                var sessionCode = json.TryGetProperty("sessionCode", out var sc)
                    ? sc.GetString() ?? ""
                    : "";

                if (!string.IsNullOrWhiteSpace(sessionCode))
                {
                    await hub.Clients
                        .Group(sessionCode)
                        .SendAsync("brokerEvent", ea.RoutingKey, json, ct);
                }

                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to relay broker event {Key}", ea.RoutingKey);
                await ch.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
        };

        await ch.BasicConsumeAsync(q.QueueName, autoAck: false, consumer, ct);
        log.LogInformation("RabbitMQ consumer started, listening on {Count} patterns", BindingKeys.Length);

        await Task.Delay(Timeout.Infinite, ct);
    }
}
