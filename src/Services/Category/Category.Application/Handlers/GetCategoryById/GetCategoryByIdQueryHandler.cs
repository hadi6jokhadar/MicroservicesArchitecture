using IhsanDev.Shared.Infrastructure.Services.Cache;
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

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public GetCategoryByIdQueryHandler(
        ICategoryRepository repository,
        CategoryFileManagerHelper fileManagerHelper,
        ICacheService cache)
    {
        _repository = repository;
        _fileManagerHelper = fileManagerHelper;
        _cache = cache;
    }

    public async Task<CategoryDto?> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"categories:id:{request.Id}";

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
