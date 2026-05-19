using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Category.Application.Events;
using Category.Application.Queries;
using Category.Domain.Interfaces;

namespace Category.Application.Handlers.GetCategorySnapshot;

/// <summary>
/// Returns every non-archived category mapped to <see cref="CategoryEventMessage"/>.
/// This reuses the same shape as pub/sub events so consumers can call this endpoint
/// on startup and upsert the results into their local snapshot table using the exact
/// same code path as the real-time subscriber.
/// </summary>
public class GetCategorySnapshotQueryHandler : IRequestHandler<GetCategorySnapshotQuery, List<CategoryEventMessage>>
{
    private readonly ICategoryRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCategorySnapshotQueryHandler(
        ICategoryRepository repository,
        ITenantContext tenantContext)
    {
        _repository    = repository;
        _tenantContext = tenantContext;
    }

    public async Task<List<CategoryEventMessage>> Handle(
        GetCategorySnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllFlatAsync(cancellationToken);
        var tenantId = _tenantContext.TenantId;

        return entities.Select(e => new CategoryEventMessage
        {
            SchemaVersion    = CategoryEventMessage.CurrentSchemaVersion,
            EventType        = CategoryEventType.Created, // treat snapshot rows as "upsert"
            TenantId         = tenantId,
            Id               = e.Id,
            Slug             = e.Slug,
            Uri              = e.Uri,
            ParentId         = e.ParentId,
            Path             = e.Path,
            Depth            = e.Depth,
            IconName         = e.IconName,
            IconFileId       = e.IconFileId,
            ImageFileId      = e.ImageFileId,
            NameTranslations = e.NameTranslations.Translations
                                .ToDictionary(kv => kv.Key, kv => kv.Value),
            OccurredAt       = DateTimeOffset.UtcNow
        }).ToList();
    }
}
