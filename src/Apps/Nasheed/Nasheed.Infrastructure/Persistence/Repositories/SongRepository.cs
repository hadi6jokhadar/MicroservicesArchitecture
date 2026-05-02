using IhsanDev.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class SongRepository : Repository<SongEntity>, ISongRepository
{
    public SongRepository(NasheedDbContext context) : base(context) { }

    public async Task<(List<SongEntity> Items, int TotalCount)> GetAllAsync(
        string? textFilter = null,
        int? artistId = null,
        SongState? state = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(s => s.Artist)
            .Where(e => !e.IsArchived);

        if (!string.IsNullOrWhiteSpace(textFilter))
            query = query.Where(e => e.Title.Contains(textFilter));

        if (artistId.HasValue)
            query = query.Where(e => e.ArtistId == artistId.Value);

        if (state.HasValue)
            query = query.Where(e => e.SongState == state.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<SongEntity>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(s => s.Artist)
            .Where(e => ids.Contains(e.Id) && !e.IsArchived)
            .ToListAsync(cancellationToken);
    }
}
