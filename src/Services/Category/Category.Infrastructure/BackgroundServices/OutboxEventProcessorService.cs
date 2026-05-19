using Category.Domain.Entities;
using Category.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Category.Infrastructure.BackgroundServices;

/// <summary>
/// Polls the <c>category_outbox_events</c> table for unprocessed rows and publishes
/// each to Redis Pub/Sub. Marks events as delivered on success, or increments
/// <see cref="OutboxEventEntity.RetryCount"/> and records the error on failure.
///
/// Events that exceed <see cref="MaxRetries"/> are marked with a future
/// <see cref="OutboxEventEntity.ProcessedAt"/> timestamp so they are excluded from
/// normal queries — a dead-letter log entry is emitted for manual investigation.
/// </summary>
public sealed class OutboxEventProcessorService : BackgroundService
{
    private const int BatchSize = 100;
    private const int MaxRetries = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<OutboxEventProcessorService> _logger;
    private readonly TimeSpan _pollInterval;

    public OutboxEventProcessorService(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer multiplexer,
        ILogger<OutboxEventProcessorService> logger,
        TimeSpan? pollInterval = null)
    {
        _scopeFactory  = scopeFactory;
        _multiplexer   = multiplexer;
        _logger        = logger;
        _pollInterval  = pollInterval ?? TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxEventProcessorService started. Poll interval: {Interval}s", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in OutboxEventProcessorService. Will retry after poll interval.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxEventProcessorService stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
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

        _logger.LogDebug("OutboxEventProcessor: processing {Count} pending event(s)", pending.Count);

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
                    // Move to dead-letter state: mark as processed (with current time) so it
                    // no longer blocks the queue. The error is retained for investigation.
                    outboxEvent.ProcessedAt = DateTimeOffset.UtcNow;

                    _logger.LogError(ex,
                        "Outbox event {Id} (channel: {Channel}) reached max retries ({MaxRetries}). " +
                        "Marking as dead-lettered. Manual intervention may be required.",
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
