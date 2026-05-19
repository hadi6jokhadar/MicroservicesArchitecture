using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Category.Application.DTOs;
using Category.Application.Helpers;
using Category.Application.Queries;
using Category.Domain.Interfaces;

namespace Category.Application.Handlers.GetCategoryList;

public class GetCategoryListQueryHandler : IRequestHandler<GetCategoryListQuery, PaginatedList<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly CategoryFileManagerHelper _fileManagerHelper;
    private readonly ICacheService _cache;
    private readonly ITenantContext _tenantContext;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public GetCategoryListQueryHandler(
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

    public async Task<PaginatedList<CategoryDto>> Handle(GetCategoryListQuery request, CancellationToken cancellationToken)
    {
        var tenantKey = _tenantContext.TenantId ?? "global";
        var cacheKey = $"categories:list:{tenantKey}:p{request.PageNumber}:s{request.PageSize}:f{request.TextFilter ?? ""}";

        var cached = await _cache.GetAsync<PaginatedList<CategoryDto>>(cacheKey, cancellationToken);
        if (cached != null)
            return cached;

        var (items, total) = await _repository.GetAllAsync(
            request.TextFilter, request.PageNumber, request.PageSize, cancellationToken);

        var result = new PaginatedList<CategoryDto>
        {
            Items = items.Select(CategoryDto.MapFrom).ToList(),
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        await _fileManagerHelper.EnrichCategoriesWithFilesAsync(result.Items, cancellationToken);
        await _cache.SetAsync(cacheKey, result, CacheTtl, cancellationToken);
        return result;
    }
}
