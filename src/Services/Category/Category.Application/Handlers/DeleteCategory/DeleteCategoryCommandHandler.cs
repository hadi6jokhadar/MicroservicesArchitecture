using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Logging;
using Category.Application.Commands;
using Category.Domain.Interfaces;

namespace Category.Application.Handlers.DeleteCategory;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, bool>
{
    private readonly ICategoryRepository _repository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;
    private readonly ICacheService _cache;
    private readonly ILogger<DeleteCategoryCommandHandler> _logger;

    public DeleteCategoryCommandHandler(
        ICategoryRepository repository,
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext,
        ICacheService cache,
        ILogger<DeleteCategoryCommandHandler> logger)
    {
        _repository = repository;
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Category with Id '{request.Id}' not found.");

        await _repository.DeleteAsync(entity, cancellationToken);

        _logger.LogInformation("Deleted Category Id {Id}", entity.Id);

        // Release file usages so File Manager can clean up temp files
        var tenantId = _tenantContext.TenantId;

        if (entity.IconFileId.HasValue)
        {
            var success = await _fileManagerClient.ChangeTempStatusAsync(entity.IconFileId.Value, "Category", entity.Id.ToString(), false, tenantId, cancellationToken);
            if (!success)
                _logger.LogWarning("Failed to release IconFileId {FileId} after deleting Category {CategoryId}", entity.IconFileId.Value, entity.Id);
        }

        if (entity.ImageFileId.HasValue && entity.ImageFileId != entity.IconFileId)
        {
            var success = await _fileManagerClient.ChangeTempStatusAsync(entity.ImageFileId.Value, "Category", entity.Id.ToString(), false, tenantId, cancellationToken);
            if (!success)
                _logger.LogWarning("Failed to release ImageFileId {FileId} after deleting Category {CategoryId}", entity.ImageFileId.Value, entity.Id);
        }

        await _cache.RemoveByPatternAsync("categories:tree*", cancellationToken);
        await _cache.RemoveByPatternAsync("categories:list*", cancellationToken);
        await _cache.RemoveAsync($"categories:id:{request.Id}", cancellationToken);

        return true;
    }
}
