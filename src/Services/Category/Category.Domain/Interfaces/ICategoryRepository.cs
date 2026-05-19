using IhsanDev.Shared.Infrastructure.Persistence;
using Category.Domain.Entities;

namespace Category.Domain.Interfaces;

public interface ICategoryRepository : IRepository<CategoryEntity>
{
    Task<(List<CategoryEntity> Items, int TotalCount)> GetAllAsync(
        string? textFilter = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all non-archived categories without pagination. Used for startup snapshot sync.</summary>
    Task<List<CategoryEntity>> GetAllFlatAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all root nodes (parentId == null) with their full subtree loaded.</summary>
    Task<List<CategoryEntity>> GetFullTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all nodes whose Path starts with the given ancestor path.</summary>
    Task<List<CategoryEntity>> GetSubtreeAsync(int rootId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a slug is already taken (excluding the given id).</summary>
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a URI is already taken (excluding the given id).</summary>
    Task<bool> UriExistsAsync(string uri, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Returns full ancestor chain ordered from root to the given node.</summary>
    Task<List<CategoryEntity>> GetAncestorsAsync(int id, CancellationToken cancellationToken = default);
}
