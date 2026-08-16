namespace Nasheed.Application.DTOs;

public record RetryAllFailedIngestionJobsResultDto(int RetriedCount, int SkippedCount);

public record RemoveAllFailedIngestionJobsResultDto(int RemovedCount);
