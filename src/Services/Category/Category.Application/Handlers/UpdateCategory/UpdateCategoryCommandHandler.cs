using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Dto;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Logging;
using Category.Application.Commands;
using Category.Application.DTOs;
using Category.Application.Events;
using Category.Application.Helpers;
using Category.Domain.Interfaces;

namespace Category.Application.Handlers.UpdateCategory;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _repository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly CategoryFileManagerHelper _fileManagerHelper;
    private readonly ITenantContext _tenantContext;
    private readonly ICacheService _cache;
    private readonly ICategoryEventPublisher _eventPublisher;
    private readonly ILogger<UpdateCategoryCommandHandler> _logger;

    public UpdateCategoryCommandHandler(
        ICategoryRepository repository,
        IFileManagerServiceClient fileManagerClient,
        CategoryFileManagerHelper fileManagerHelper,
        ITenantContext tenantContext,
        ICacheService cache,
        ICategoryEventPublisher eventPublisher,
        ILogger<UpdateCategoryCommandHandler> logger)
    {
        _repository = repository;
        _fileManagerClient = fileManagerClient;
        _fileManagerHelper = fileManagerHelper;
        _tenantContext = tenantContext;
        _cache = cache;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<CategoryDto> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Category with Id '{request.Id}' not found.");

        if (request.Slug != null &&
            await _repository.SlugExistsAsync(request.Slug, excludeId: request.Id, cancellationToken: cancellationToken))
            throw new ConflictException($"Slug '{request.Slug}' is already in use.");

        if (request.Uri != null &&
            await _repository.UriExistsAsync(request.Uri, excludeId: request.Id, cancellationToken: cancellationToken))
            throw new ConflictException($"Uri '{request.Uri}' is already in use.");

        var oldIconFileId = entity.IconFileId;
        var oldImageFileId = entity.ImageFileId;

        LocalizedMapping? nameTranslations = request.NameTranslations != null
            ? LocalizedMapping.From(request.NameTranslations)
            : null;

        entity.Update(
            slug: request.Slug,
            uri: request.Uri,
            nameTranslations: nameTranslations,
            iconFileId: request.IconFileId,
            imageFileId: request.ImageFileId,
            iconName: request.IconName,
            attributes: request.Attributes);

        // If Uri changed, recalculate the path (parent path remains the same)
        if (request.Uri != null)
        {
            var parentPath = entity.ParentId.HasValue
                ? (await _repository.GetByIdAsync(entity.ParentId.Value, cancellationToken))?.Path
                : null;
            entity.RecalculatePath(parentPath);
        }

        // Queue the outbox event BEFORE UpdateAsync so both the entity change and the
        // outbox row are committed in the same SaveChangesAsync call — true atomicity.
        var tenantId = _tenantContext.TenantId;
        await _eventPublisher.PublishAsync(entity, CategoryEventType.Updated, tenantId, cancellationToken);
        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("Updated Category Id {Id}", entity.Id);

        // Handle file usage tracking changes

        if (request.IconFileId.HasValue && oldIconFileId != request.IconFileId)
        {
            if (oldIconFileId.HasValue)
            {
                var success = await _fileManagerClient.ChangeTempStatusAsync(oldIconFileId.Value, "Category", entity.Id.ToString(), false, tenantId, cancellationToken);
                if (!success)
                    _logger.LogWarning("Failed to remove usage for old IconFileId {FileId} for Category {CategoryId}", oldIconFileId.Value, entity.Id);
            }
            var addSuccess = await _fileManagerClient.ChangeTempStatusAsync(request.IconFileId.Value, "Category", entity.Id.ToString(), true, tenantId, cancellationToken);
            if (!addSuccess)
                _logger.LogWarning("Failed to add usage for new IconFileId {FileId} for Category {CategoryId}", request.IconFileId.Value, entity.Id);
        }

        if (request.ImageFileId.HasValue && oldImageFileId != request.ImageFileId)
        {
            if (oldImageFileId.HasValue)
            {
                var success = await _fileManagerClient.ChangeTempStatusAsync(oldImageFileId.Value, "Category", entity.Id.ToString(), false, tenantId, cancellationToken);
                if (!success)
                    _logger.LogWarning("Failed to remove usage for old ImageFileId {FileId} for Category {CategoryId}", oldImageFileId.Value, entity.Id);
            }
            var addSuccess = await _fileManagerClient.ChangeTempStatusAsync(request.ImageFileId.Value, "Category", entity.Id.ToString(), true, tenantId, cancellationToken);
            if (!addSuccess)
                _logger.LogWarning("Failed to add usage for new ImageFileId {FileId} for Category {CategoryId}", request.ImageFileId.Value, entity.Id);
        }

        await _cache.RemoveByPatternAsync("categories:tree*", cancellationToken);
        await _cache.RemoveByPatternAsync("categories:list*", cancellationToken);
        await _cache.RemoveAsync($"categories:id:{request.Id}", cancellationToken);

        var dto = CategoryDto.MapFrom(entity);
        await _fileManagerHelper.EnrichCategoryWithFilesAsync(dto, cancellationToken);
        return dto;
    }
}
