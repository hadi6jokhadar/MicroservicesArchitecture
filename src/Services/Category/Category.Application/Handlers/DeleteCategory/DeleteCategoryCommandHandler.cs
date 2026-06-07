using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Category.Application.Commands;
using Category.Application.Events;
using Category.Domain.Interfaces;

namespace Category.Application.Handlers.DeleteCategory;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, bool>
{
    private readonly ICategoryRepository _repository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;
    private readonly ICacheService _cache;
    private readonly ICategoryEventPublisher _eventPublisher;
    private readonly ILogger<DeleteCategoryCommandHandler> _logger;

    public DeleteCategoryCommandHandler(
        ICategoryRepository repository,
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext,
        ICacheService cache,
        ICategoryEventPublisher eventPublisher,
        ILogger<DeleteCategoryCommandHandler> logger)
    {
        _repository = repository;
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
        _cache = cache;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Category with Id '{request.Id}' not found.");

        // Queue the outbox event BEFORE DeleteAsync so both the soft-delete and the
        // outbox row are committed in the same SaveChangesAsync call — true atomicity.
        await _eventPublisher.PublishAsync(entity, CategoryEventType.Deleted, _tenantContext.TenantId, cancellationToken);
        await _repository.DeleteAsync(entity, cancellationToken);

        _logger.LogInformation("Deleted Category Id {Id}", entity.Id);

        // Release file usages so File Manager can clean up temp files
        var tenantId = _tenantContext.TenantId;

        if (entity.IconFileId.HasValue)
        {
            try
            {
                var success = await _fileManagerClient.ChangeTempStatusAsync(entity.IconFileId.Value, "Category", entity.Id.ToString(), false, tenantId, cancellationToken);
                if (!success)
                    _logger.LogWarning("Failed to release IconFileId {FileId} after deleting Category {CategoryId}", entity.IconFileId.Value, entity.Id);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "FileManager circuit open; skipping IconFileId {FileId} release for Category {CategoryId}", entity.IconFileId.Value, entity.Id);
            }
        }

        if (entity.ImageFileId.HasValue && entity.ImageFileId != entity.IconFileId)
        {
            try
            {
                var success = await _fileManagerClient.ChangeTempStatusAsync(entity.ImageFileId.Value, "Category", entity.Id.ToString(), false, tenantId, cancellationToken);
                if (!success)
                    _logger.LogWarning("Failed to release ImageFileId {FileId} after deleting Category {CategoryId}", entity.ImageFileId.Value, entity.Id);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "FileManager circuit open; skipping ImageFileId {FileId} release for Category {CategoryId}", entity.ImageFileId.Value, entity.Id);
            }
        }

        await _cache.RemoveByPatternAsync("categories:tree*", cancellationToken);
        await _cache.RemoveByPatternAsync("categories:list*", cancellationToken);
        await _cache.RemoveAsync($"categories:id:{request.Id}", cancellationToken);

        return true;
    }
}
