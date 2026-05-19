using IhsanDev.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Category.Domain.Entities;
using Category.Domain.Interfaces;
using Category.Infrastructure.Persistence;

namespace Category.Infrastructure.Persistence.Repositories;

public class CategoryRepository : Repository<CategoryEntity>, ICategoryRepository
{
    public CategoryRepository(CategoryDbContext context) : base(context) { }

    public async Task<(List<CategoryEntity> Items, int TotalCount)> GetAllAsync(
        string? textFilter = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(e => !e.IsArchived);

        if (!string.IsNullOrWhiteSpace(textFilter))
            query = query.Where(e => e.Slug.Contains(textFilter));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.Path)
            .ThenBy(e => e.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<CategoryEntity>> GetFullTreeAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(e => !e.IsArchived)
            .OrderBy(e => e.Depth)
            .ThenBy(e => e.Path)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CategoryEntity>> GetAllFlatAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(e => !e.IsArchived)
            .OrderBy(e => e.Depth)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CategoryEntity>> GetSubtreeAsync(int rootId, CancellationToken cancellationToken = default)
    {
        // First get the root to know its path
        var root = await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == rootId, cancellationToken);

        if (root == null)
            return new List<CategoryEntity>();

        var rootPath = root.Path;

        // Return root + all descendants (path starts with root path)
        return await _dbSet
            .AsNoTracking()
            .Where(e => e.Path.StartsWith(rootPath) && !e.IsArchived)
            .OrderBy(e => e.Depth)
            .ThenBy(e => e.Path)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        string slug,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(e => e.Slug == slug && !e.IsArchived);
        if (excludeId.HasValue)
            query = query.Where(e => e.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> UriExistsAsync(
        string uri,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(e => e.Uri == uri && !e.IsArchived);
        if (excludeId.HasValue)
            query = query.Where(e => e.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<List<CategoryEntity>> GetAncestorsAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity == null)
            return new List<CategoryEntity>();

        // Parse ancestor URI segments from materialized path e.g. "/electronics/phones/iphone/"
        // → ["electronics", "phones"]  (exclude the node itself at the last segment)
        var pathSegments = entity.Path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Remove the last segment (the entity's own URI)
        if (pathSegments.Count > 0)
            pathSegments.RemoveAt(pathSegments.Count - 1);

        if (pathSegments.Count == 0)
            return new List<CategoryEntity>();

        return await _dbSet
            .AsNoTracking()
            .Where(e => pathSegments.Contains(e.Uri))
            .OrderBy(e => e.Depth)
            .ToListAsync(cancellationToken);
    }
}
