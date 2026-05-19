using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Logging;
using Category.Application.Commands;
using Category.Application.DTOs;
using Category.Application.Events;
using Category.Domain.Interfaces;

namespace Category.Application.Handlers.MoveCategory;

/// <summary>
/// Moves a category node to a new parent.
/// Updates path and depth for the moved node and all its descendants.
/// </summary>
public class MoveCategoryCommandHandler : IRequestHandler<MoveCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _repository;
    private readonly ICacheService _cache;
    private readonly ICategoryEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MoveCategoryCommandHandler> _logger;

    public MoveCategoryCommandHandler(
        ICategoryRepository repository,
        ICacheService cache,
        ICategoryEventPublisher eventPublisher,
        ITenantContext tenantContext,
        ILogger<MoveCategoryCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _eventPublisher = eventPublisher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<CategoryDto> Handle(MoveCategoryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Category with Id '{request.Id}' not found.");

        string? newParentPath = null;
        int newParentDepth = -1;

        if (request.NewParentId.HasValue)
        {
            var newParent = await _repository.GetByIdAsync(request.NewParentId.Value, cancellationToken)
                ?? throw new NotFoundException($"Target parent category with Id '{request.NewParentId}' not found.");

            // Prevent moving into own subtree
            if (newParent.Path.StartsWith(entity.Path))
                throw new BadRequestException("Cannot move a category into one of its own descendants.");

            newParentPath = newParent.Path;
            newParentDepth = newParent.Depth;
        }

        var oldPath = entity.Path;

        entity.MoveTo(request.NewParentId, newParentPath, newParentDepth);
        entity.RecalculatePath(newParentPath);

        // Queue the outbox event BEFORE UpdateAsync so both the entity change and the
        // outbox row are committed in the same SaveChangesAsync call — true atomicity.
        await _eventPublisher.PublishAsync(entity, CategoryEventType.Updated, _tenantContext.TenantId, cancellationToken);
        await _repository.UpdateAsync(entity, cancellationToken);

        // Propagate path/depth changes to all descendants
        var descendants = await _repository.GetSubtreeAsync(request.Id, cancellationToken);
        foreach (var descendant in descendants.Where(d => d.Id != request.Id))
        {
            // Replace old path prefix with new path
            var newDescPath = descendant.Path.Replace(oldPath, entity.Path, StringComparison.Ordinal);
            var newDepth = newDescPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length - 1;
            descendant.MoveTo(descendant.ParentId, newDescPath, newDepth - 1);
            descendant.RecalculatePath(
                newDescPath.LastIndexOf('/') > 0
                    ? newDescPath[..newDescPath.LastIndexOf('/', newDescPath.Length - 2)]
                    : null);
            await _repository.UpdateAsync(descendant, cancellationToken);
        }

        _logger.LogInformation("Moved Category Id {Id} to parent {ParentId}", entity.Id, request.NewParentId);

        await _cache.RemoveByPatternAsync("categories:tree*", cancellationToken);
        await _cache.RemoveByPatternAsync("categories:list*", cancellationToken);
        await _cache.RemoveAsync($"categories:id:{request.Id}", cancellationToken);

        return CategoryDto.MapFrom(entity);
    }
}
