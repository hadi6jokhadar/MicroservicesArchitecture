using Category.Domain.Entities;
using Category.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Category.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that replaces the polling loop in <c>OutboxEventProcessorService</c>.
/// Fires every 5 seconds (registered via Hangfire cron) — reads unprocessed outbox rows
/// and publishes each to Redis Pub/Sub with the same retry / dead-letter logic.
/// </summary>
public class OutboxEventProcessorJob
{
    private const int BatchSize = 100;
    private const int MaxRetries = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<OutboxEventProcessorJob> _logger;

    public OutboxEventProcessorJob(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer multiplexer,
        ILogger<OutboxEventProcessorJob> logger)
    {
        _scopeFactory  = scopeFactory;
        _multiplexer   = multiplexer;
        _logger        = logger;
    }

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CategoryDbContext>();

        var pending = await db.OutboxEvents
            .Where(e => e.ProcessedAt == null && e.RetryCount < MaxRetries)
            .OrderBy(e => e.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        _logger.LogDebug("OutboxEventProcessorJob: processing {Count} pending event(s)", pending.Count);

        var sub = _multiplexer.GetSubscriber();

        foreach (var outboxEvent in pending)
        {
            try
            {
                var channel = new RedisChannel(outboxEvent.Channel, RedisChannel.PatternMode.Literal);
                await sub.PublishAsync(channel, outboxEvent.Payload);

                outboxEvent.ProcessedAt = DateTimeOffset.UtcNow;

                _logger.LogDebug(
                    "Outbox event {Id} published to channel {Channel}",
                    outboxEvent.Id, outboxEvent.Channel);
            }
            catch (Exception ex)
            {
                outboxEvent.RetryCount++;
                outboxEvent.LastError = ex.Message;

                if (outboxEvent.RetryCount >= MaxRetries)
                {
                    outboxEvent.ProcessedAt = DateTimeOffset.UtcNow;
                    _logger.LogError(ex,
                        "Outbox event {Id} (channel: {Channel}) reached max retries ({MaxRetries}). Marking as dead-lettered.",
                        outboxEvent.Id, outboxEvent.Channel, MaxRetries);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "Outbox event {Id} (channel: {Channel}) failed publish attempt {RetryCount}/{MaxRetries}.",
                        outboxEvent.Id, outboxEvent.Channel, outboxEvent.RetryCount, MaxRetries);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
