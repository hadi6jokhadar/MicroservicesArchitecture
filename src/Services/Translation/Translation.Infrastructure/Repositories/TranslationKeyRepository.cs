using Translation.Domain.Entities;
using Translation.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using IhsanDev.Shared.Infrastructure.Persistence;
using Translation.Infrastructure.Persistence;

namespace Translation.Infrastructure.Repositories;

public class TranslationKeyRepository : Repository<TranslationKey>, ITranslationKeyRepository
{
    public TranslationKeyRepository(TranslationDbContext context) : base(context)
    {
    }

    public async Task<TranslationKey?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Key == key && !t.IsArchived, cancellationToken);
    }

    public async Task<IEnumerable<TranslationKey>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t => t.Category == category && !t.IsArchived)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(t => t.Key == key && !t.IsArchived, cancellationToken);
    }

    public async Task<(List<TranslationKey> Items, int TotalCount)> GetPaginatedAsync(
        int pageNumber = 1,
        int pageSize = 10,
        string? category = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(t => !t.IsArchived);

        // Apply category filter
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category == category);
        }

        // Apply search term filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            query = query.Where(t =>
                t.Key.ToLower().Contains(lowerSearchTerm) ||
                (t.Description != null && t.Description.ToLower().Contains(lowerSearchTerm)));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply ordering and pagination
        var items = await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Key)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.Values)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
