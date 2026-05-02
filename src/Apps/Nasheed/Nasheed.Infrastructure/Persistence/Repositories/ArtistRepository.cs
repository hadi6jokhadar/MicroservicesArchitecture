using IhsanDev.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class ArtistRepository : Repository<ArtistEntity>, IArtistRepository
{
    public ArtistRepository(NasheedDbContext context) : base(context) { }

    public async Task<(List<ArtistEntity> Items, int TotalCount)> GetAllAsync(
        string? textFilter = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(e => !e.IsArchived);

        if (!string.IsNullOrWhiteSpace(textFilter))
            query = query.Where(e => e.Name.Contains(textFilter));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
