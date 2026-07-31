using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Category.Application.DTOs;
using Category.Application.Helpers;
using Category.Application.Queries;
using Category.Domain.Interfaces;

namespace Category.Application.Handlers.GetCategoryById;

public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, CategoryDto?>
{
    private readonly ICategoryRepository _repository;
    private readonly CategoryFileManagerHelper _fileManagerHelper;
    private readonly ICacheService _cache;
    private readonly ITenantContext _tenantContext;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public GetCategoryByIdQueryHandler(
        ICategoryRepository repository,
        CategoryFileManagerHelper fileManagerHelper,
        ICacheService cache,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _fileManagerHelper = fileManagerHelper;
        _cache = cache;
        _tenantContext = tenantContext;
    }

    public async Task<CategoryDto?> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        // Redis is one shared instance across all tenants and category IDs are independent
        // per-tenant auto-increment integers, so the tenant component is required here — without
        // it, Tenant A's cached category N is served straight back to Tenant B's own category N.
        var tenantKey = _tenantContext.TenantId ?? "global";
        var cacheKey = $"categories:id:{request.Id}:{tenantKey}";

        var cached = await _cache.GetAsync<CategoryDto>(cacheKey, cancellationToken);
        if (cached != null)
            return cached;

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
            return null;

        var dto = CategoryDto.MapFrom(entity);
        await _fileManagerHelper.EnrichCategoryWithFilesAsync(dto, cancellationToken);
        await _cache.SetAsync(cacheKey, dto, CacheTtl, cancellationToken);
        return dto;
    }
}
