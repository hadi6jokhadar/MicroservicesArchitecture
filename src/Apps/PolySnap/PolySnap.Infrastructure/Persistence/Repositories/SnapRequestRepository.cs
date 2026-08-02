using IhsanDev.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PolySnap.Domain.Entities;
using PolySnap.Domain.Interfaces;
using PolySnap.Infrastructure.Persistence;

namespace PolySnap.Infrastructure.Persistence.Repositories;

public class SnapRequestRepository : Repository<SnapRequestEntity>, ISnapRequestRepository
{
    public SnapRequestRepository(PolySnapDbContext context) : base(context) { }

    public async Task<(List<SnapRequestEntity> Items, int TotalCount)> GetAllAsync(
        string? textFilter = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(e => !e.IsArchived);

        if (!string.IsNullOrWhiteSpace(textFilter))
        {
            var escaped = textFilter.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(e => EF.Functions.Like(e.Name, $"%{escaped}%", "\\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
