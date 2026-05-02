using IhsanDev.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class SongIngestionJobRepository : Repository<SongIngestionJobEntity>, ISongIngestionJobRepository
{
    public SongIngestionJobRepository(NasheedDbContext context) : base(context) { }

    public async Task<(List<SongIngestionJobEntity> Items, int TotalCount)> GetAllAsync(
        int? songId = null,
        IngestionJobStatus? status = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(e => !e.IsArchived);

        if (songId.HasValue)
            query = query.Where(e => e.SongId == songId.Value);

        if (status.HasValue)
            query = query.Where(e => e.JobStatus == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<SongIngestionJobEntity>> GetPendingJobsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => !e.IsArchived
                && (e.JobStatus == IngestionJobStatus.Pending)
                && (e.NextRetryAt == null || e.NextRetryAt <= DateTime.UtcNow))
            .OrderBy(e => e.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<SongIngestionJobEntity?> GetBySongIdAsync(int songId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => e.SongId == songId && !e.IsArchived)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
