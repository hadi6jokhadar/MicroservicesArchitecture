using IhsanDev.Shared.Infrastructure.Persistence;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;

namespace Nasheed.Domain.Interfaces;

public interface ISongIngestionJobRepository : IRepository<SongIngestionJobEntity>
{
    Task<(List<SongIngestionJobEntity> Items, int TotalCount)> GetAllAsync(
        int? songId = null,
        IngestionJobStatus? status = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>Returns pending jobs that are due for processing (NextRetryAt is null or in the past).</summary>
    Task<List<SongIngestionJobEntity>> GetPendingJobsAsync(int batchSize, CancellationToken cancellationToken = default);

    Task<SongIngestionJobEntity?> GetBySongIdAsync(int songId, CancellationToken cancellationToken = default);
}
