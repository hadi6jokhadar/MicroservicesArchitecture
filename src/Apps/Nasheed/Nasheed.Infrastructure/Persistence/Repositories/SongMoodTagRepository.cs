using IhsanDev.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class SongMoodTagRepository : Repository<SongMoodTagEntity>, ISongMoodTagRepository
{
    public SongMoodTagRepository(NasheedDbContext context) : base(context) { }

    public async Task<List<SongMoodTagEntity>> GetBySongIdAsync(int songId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.SongId == songId && !t.IsArchived)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteBySongIdAsync(int songId, CancellationToken cancellationToken = default)
    {
        var tags = await _dbSet.Where(t => t.SongId == songId).ToListAsync(cancellationToken);
        _dbSet.RemoveRange(tags);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
