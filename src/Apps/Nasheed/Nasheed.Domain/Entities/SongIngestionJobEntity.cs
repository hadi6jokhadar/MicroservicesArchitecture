using IhsanDev.Shared.Kernel.Entities;
using Nasheed.Domain.Enums;

namespace Nasheed.Domain.Entities;

public class SongIngestionJobEntity : BaseEntity
{
    public int SongId { get; private set; }
    public string FileId { get; private set; } = string.Empty;
    public IngestionJobType JobType { get; private set; }
    public IngestionJobStatus JobStatus { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? RemovedAt { get; private set; }

    // Navigation
    public SongEntity? Song { get; private set; }

    private SongIngestionJobEntity() { }

    public static SongIngestionJobEntity Create(int songId, string fileId, IngestionJobType jobType, int maxRetries = 3)
    {
        return new SongIngestionJobEntity
        {
            SongId = songId,
            FileId = fileId,
            JobType = jobType,
            JobStatus = IngestionJobStatus.Pending,
            RetryCount = 0,
            MaxRetries = maxRetries
        };
    }

    public void MarkRunning()
    {
        JobStatus = IngestionJobStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        JobStatus = IngestionJobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        LastError = null;
    }

    public void MarkFailed(string error, DateTime? nextRetryAt = null)
    {
        RetryCount++;
        LastError = error;
        NextRetryAt = nextRetryAt;
        JobStatus = RetryCount >= MaxRetries ? IngestionJobStatus.Failed : IngestionJobStatus.Pending;
    }

    public void MarkRemoved()
    {
        JobStatus = IngestionJobStatus.Removed;
        RemovedAt = DateTime.UtcNow;
    }

    public void ResetForRetry()
    {
        JobStatus = IngestionJobStatus.Pending;
        NextRetryAt = null;
        LastError = null;
    }
}
