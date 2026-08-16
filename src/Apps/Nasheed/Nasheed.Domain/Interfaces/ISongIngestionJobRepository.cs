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

    /// <summary>Returns every non-archived job with the given status, unpaginated — used by bulk
    /// admin actions (retry-all / remove-all failed jobs).</summary>
    Task<List<SongIngestionJobEntity>> GetByStatusAsync(IngestionJobStatus status, CancellationToken cancellationToken = default);

    /// <summary>Returns true if another job of the same (SongId, JobType) is Pending or Running.
    /// Pass <paramref name="excludeJobId"/> when checking around a specific job that is itself
    /// allowed to be in an active state (e.g. re-validating before resetting that same job).</summary>
    Task<bool> HasActiveJobAsync(
        int songId,
        IngestionJobType jobType,
        CancellationToken cancellationToken = default,
        int? excludeJobId = null);

    Task<SongIngestionJobEntity?> GetBySongIdAsync(int songId, CancellationToken cancellationToken = default);
    Task DeleteBySongIdAsync(int songId, CancellationToken cancellationToken = default);
}
