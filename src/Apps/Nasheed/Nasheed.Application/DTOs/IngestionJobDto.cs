using System.Globalization;
using IhsanDev.Shared.Kernel.Dto.Identity;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;

namespace Nasheed.Application.DTOs;

public class IngestionJobDto : BaseDto
{
    public int SongId { get; set; }
    public string FileId { get; set; } = string.Empty;
    public IngestionJobType JobType { get; set; }
    public IngestionJobStatus JobStatus { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public string? LastError { get; set; }
    public string? NextRetryAt { get; set; }
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string? RemovedAt { get; set; }

    public static IngestionJobDto MapFrom(SongIngestionJobEntity entity) => new()
    {
        Id = entity.Id,
        SongId = entity.SongId,
        FileId = entity.FileId,
        JobType = entity.JobType,
        JobStatus = entity.JobStatus,
        RetryCount = entity.RetryCount,
        MaxRetries = entity.MaxRetries,
        LastError = entity.LastError,
        NextRetryAt = entity.NextRetryAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        StartedAt = entity.StartedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        CompletedAt = entity.CompletedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        RemovedAt = entity.RemovedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        Status = entity.Status,
        IsArchived = entity.IsArchived,
        Created = entity.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        LastModified = entity.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
