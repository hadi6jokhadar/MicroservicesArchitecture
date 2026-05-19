using Category.Domain.Entities;

namespace Category.Application.Events;

/// <summary>
/// Publishes category change events so consumer services can maintain
/// a local read-only snapshot without calling this service at runtime.
/// </summary>
public interface ICategoryEventPublisher
{
    /// <summary>
    /// Queues an outbox event for the given entity and event type in the EF change tracker.
    /// The caller must call SaveChangesAsync via the repository immediately after so the
    /// outbox row and the entity mutation are committed in the same database transaction.
    /// </summary>
    Task PublishAsync(CategoryEntity entity, CategoryEventType eventType, string? tenantId, CancellationToken cancellationToken = default);
}
