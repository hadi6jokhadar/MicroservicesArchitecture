using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Category.Application.DTOs;
using Category.Application.Helpers;
using Category.Application.Queries;
using Category.Domain.Interfaces;

namespace Category.Application.Handlers.GetCategoryTree;

public class GetCategoryTreeQueryHandler : IRequestHandler<GetCategoryTreeQuery, List<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly CategoryFileManagerHelper _fileManagerHelper;
    private readonly ICacheService _cache;
    private readonly ITenantContext _tenantContext;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public GetCategoryTreeQueryHandler(
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

    public async Task<List<CategoryDto>> Handle(GetCategoryTreeQuery request, CancellationToken cancellationToken)
    {
        var tenantKey = _tenantContext.TenantId ?? "global";

        if (string.IsNullOrWhiteSpace(request.TextFilter))
        {
            var cacheKey = $"categories:tree:full:{tenantKey}";
            var cached = await _cache.GetAsync<List<CategoryDto>>(cacheKey, cancellationToken);
            if (cached != null)
                return cached;

            var allNodes = await _repository.GetFullTreeAsync(cancellationToken);
            var flatDtos = allNodes.Select(CategoryDto.MapFrom).ToList();
            await _fileManagerHelper.EnrichCategoriesWithFilesAsync(flatDtos, cancellationToken);
            var tree = CategoryDto.BuildTree(flatDtos);

            await _cache.SetAsync(cacheKey, tree, CacheTtl, cancellationToken);
            return tree;
        }

        // Filtered query — bypass cache
        var term = request.TextFilter.Trim().ToLowerInvariant();
        var allEntities = await _repository.GetFullTreeAsync(cancellationToken);
        var allFlat = allEntities.Select(CategoryDto.MapFrom).ToList();

        // Collect IDs of nodes that match the filter
        var matchingIds = allFlat
            .Where(d => MatchesFilter(d, term))
            .Select(d => d.Id)
            .ToHashSet();

        // Include ancestors of matching nodes so the tree shape is preserved
        var dtoById = allFlat.ToDictionary(d => d.Id);
        var includedIds = new HashSet<int>(matchingIds);
        foreach (var id in matchingIds.ToArray())
        {
            var current = dtoById.GetValueOrDefault(id);
            while (current?.ParentId != null)
            {
                includedIds.Add(current.ParentId.Value);
                current = dtoById.GetValueOrDefault(current.ParentId.Value);
            }
        }

        var filteredFlat = allFlat.Where(d => includedIds.Contains(d.Id)).ToList();
        await _fileManagerHelper.EnrichCategoriesWithFilesAsync(filteredFlat, cancellationToken);
        return CategoryDto.BuildTree(filteredFlat);
    }

    private static bool MatchesFilter(CategoryDto dto, string term)
    {
        if (dto.Slug.ToLowerInvariant().Contains(term)) return true;
        foreach (var name in dto.NameTranslations.Values)
            if (name.ToLowerInvariant().Contains(term)) return true;
        return false;
    }
}
