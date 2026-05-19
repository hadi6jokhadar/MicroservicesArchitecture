using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Logging;
using Category.Application.Commands;
using Category.Application.DTOs;
using Category.Application.Events;
using Category.Application.Helpers;
using Category.Domain.Entities;
using Category.Domain.Interfaces;
using IhsanDev.Shared.Kernel.Dto;

namespace Category.Application.Handlers.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _repository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly CategoryFileManagerHelper _fileManagerHelper;
    private readonly ITenantContext _tenantContext;
    private readonly ICacheService _cache;
    private readonly ICategoryEventPublisher _eventPublisher;
    private readonly ILogger<CreateCategoryCommandHandler> _logger;

    public CreateCategoryCommandHandler(
        ICategoryRepository repository,
        IFileManagerServiceClient fileManagerClient,
        CategoryFileManagerHelper fileManagerHelper,
        ITenantContext tenantContext,
        ICacheService cache,
        ICategoryEventPublisher eventPublisher,
        ILogger<CreateCategoryCommandHandler> logger)
    {
        _repository = repository;
        _fileManagerClient = fileManagerClient;
        _fileManagerHelper = fileManagerHelper;
        _tenantContext = tenantContext;
        _cache = cache;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.SlugExistsAsync(request.Slug, cancellationToken: cancellationToken))
            throw new ConflictException($"Slug '{request.Slug}' is already in use.");

        if (await _repository.UriExistsAsync(request.Uri, cancellationToken: cancellationToken))
            throw new ConflictException($"Uri '{request.Uri}' is already in use.");

        var nameTranslations = LocalizedMapping.From(request.NameTranslations);

        string? parentPath = null;
        int parentDepth = -1;

        if (request.ParentId.HasValue)
        {
            var parent = await _repository.GetByIdAsync(request.ParentId.Value, cancellationToken)
                ?? throw new NotFoundException($"Parent category with Id '{request.ParentId}' not found.");
            parentPath = parent.Path;
            parentDepth = parent.Depth;
        }

        var entity = CategoryEntity.Create(
            slug: request.Slug,
            uri: request.Uri,
            nameTranslations: nameTranslations,
            parentId: request.ParentId,
            iconFileId: request.IconFileId,
            imageFileId: request.ImageFileId,
            iconName: request.IconName,
            attributes: request.Attributes);

        if (request.ParentId.HasValue)
            entity.SetHierarchy(request.ParentId, parentPath!, parentDepth);

        await _repository.AddAsync(entity, cancellationToken);

        // Recalculate path using Uri after entity is persisted
        entity.RecalculatePath(parentPath);

        // Queue the outbox event BEFORE UpdateAsync so both the path update and the
        // outbox row are committed in the same SaveChangesAsync call — true atomicity.
        await _eventPublisher.PublishAsync(entity, CategoryEventType.Created, _tenantContext.TenantId, cancellationToken);
        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("Created Category Id {Id}, Slug {Slug}, Uri {Uri}", entity.Id, entity.Slug, entity.Uri);

        // Mark icon file as in-use
        var tenantId = _tenantContext.TenantId;
        if (request.IconFileId.HasValue)
        {
            var success = await _fileManagerClient.ChangeTempStatusAsync(request.IconFileId.Value, "Category", entity.Id.ToString(), true, tenantId, cancellationToken);
            if (!success)
                _logger.LogWarning("Failed to mark IconFileId {FileId} as permanent for Category {CategoryId}", request.IconFileId.Value, entity.Id);
        }

        // Mark image file as in-use
        if (request.ImageFileId.HasValue && request.ImageFileId != request.IconFileId)
        {
            var success = await _fileManagerClient.ChangeTempStatusAsync(request.ImageFileId.Value, "Category", entity.Id.ToString(), true, tenantId, cancellationToken);
            if (!success)
                _logger.LogWarning("Failed to mark ImageFileId {FileId} as permanent for Category {CategoryId}", request.ImageFileId.Value, entity.Id);
        }

        // Invalidate tree caches — the tenant context will scope cache keys
        await _cache.RemoveByPatternAsync("categories:tree*", cancellationToken);
        await _cache.RemoveByPatternAsync("categories:list*", cancellationToken);

        var dto = CategoryDto.MapFrom(entity);
        await _fileManagerHelper.EnrichCategoryWithFilesAsync(dto, cancellationToken);
        return dto;
    }
}
