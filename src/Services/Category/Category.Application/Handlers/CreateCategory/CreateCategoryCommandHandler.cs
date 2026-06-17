using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
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

        // Calculate the materialized path before INSERT so the DB row has the correct path
        // from the start. Uri is already set at this point — no need to wait for the Id.
        entity.RecalculatePath(parentPath);

        await _repository.AddAsync(entity, cancellationToken);

        // Queue the outbox event and persist it atomically with any remaining entity updates.
        await _eventPublisher.PublishAsync(entity, CategoryEventType.Created, _tenantContext.TenantId, cancellationToken);
        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("Created Category Id {Id}, Slug {Slug}, Uri {Uri}", entity.Id, entity.Slug, entity.Uri);

        // Mark icon file as in-use
        var tenantId = _tenantContext.TenantId;
        if (request.IconFileId.HasValue)
        {
            try
            {
                var success = await _fileManagerClient.ChangeTempStatusAsync(request.IconFileId.Value, "Category", entity.Id.ToString(), true, tenantId, cancellationToken);
                if (!success)
                    _logger.LogWarning("Failed to mark IconFileId {FileId} as permanent for Category {CategoryId}", request.IconFileId.Value, entity.Id);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "FileManager circuit open; skipping IconFileId {FileId} mark for Category {CategoryId}", request.IconFileId.Value, entity.Id);
            }
        }

        // Mark image file as in-use
        if (request.ImageFileId.HasValue && request.ImageFileId != request.IconFileId)
        {
            try
            {
                var success = await _fileManagerClient.ChangeTempStatusAsync(request.ImageFileId.Value, "Category", entity.Id.ToString(), true, tenantId, cancellationToken);
                if (!success)
                    _logger.LogWarning("Failed to mark ImageFileId {FileId} as permanent for Category {CategoryId}", request.ImageFileId.Value, entity.Id);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "FileManager circuit open; skipping ImageFileId {FileId} mark for Category {CategoryId}", request.ImageFileId.Value, entity.Id);
            }
        }

        // Invalidate tree caches — the tenant context will scope cache keys
        await _cache.RemoveByPatternAsync("categories:tree*", cancellationToken);
        await _cache.RemoveByPatternAsync("categories:list*", cancellationToken);

        var dto = CategoryDto.MapFrom(entity);
        await _fileManagerHelper.EnrichCategoryWithFilesAsync(dto, cancellationToken);
        return dto;
    }
}
