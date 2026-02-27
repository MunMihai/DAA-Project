using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Quiz.LiveSessionService.Hubs;

namespace Quiz.LiveSessionService.Messaging;

/// <summary>
/// Background service that:
///   1. Subscribes to all livequiz.events
///   2. Relays them to the appropriate SignalR group
///
/// This enables horizontal scaling: multiple LiveSessionService instances
/// can run behind a load balancer. Each publishes events to RabbitMQ,
/// and each consumer re-broadcasts to its local SignalR connections.
///
/// For production SignalR scale-out, also add Redis backplane:
///   builder.Services.AddSignalR().AddStackExchangeRedis(connectionString)
/// </summary>
public sealed class RabbitEventConsumer(
    RabbitBus bus,
    IHubContext<LiveQuizHub> hub,
    ILogger<RabbitEventConsumer> log
) : BackgroundService
{
    // Routing key patterns to subscribe to
    private static readonly string[] BindingKeys =
    [
        "player.*",
        "session.*",
        "question.*",
        "answer.*",
        "score.*"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Retry loop for resilience
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

        // Exclusive, auto-delete queue per instance
        var q = await ch.QueueDeclareAsync(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: ct);

        foreach (var key in BindingKeys)
            await ch.QueueBindAsync(q.QueueName, "livequiz.events", key, cancellationToken: ct);

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
                    // Relay raw event to all clients in the group
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