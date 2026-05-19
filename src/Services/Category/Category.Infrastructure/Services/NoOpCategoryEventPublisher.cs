using Category.Application.Events;
using Category.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Category.Infrastructure.Services;

/// <summary>
/// No-op publisher used when Redis is disabled (local dev / in-memory cache mode).
/// Events are simply logged and dropped — no pub/sub infrastructure required.
/// </summary>
public sealed class NoOpCategoryEventPublisher : ICategoryEventPublisher
{
    private readonly ILogger<NoOpCategoryEventPublisher> _logger;

    public NoOpCategoryEventPublisher(ILogger<NoOpCategoryEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(CategoryEntity entity, CategoryEventType eventType, string? tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[NoOp] Category event {EventType} for Id {Id} (tenant: {TenantId}) not published — Redis is disabled.",
            eventType, entity.Id, tenantId ?? "global");
        return Task.CompletedTask;
    }
}
