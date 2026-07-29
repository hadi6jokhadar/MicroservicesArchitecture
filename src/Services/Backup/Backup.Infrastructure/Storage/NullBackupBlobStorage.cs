using Backup.Application.Interfaces;

namespace Backup.Infrastructure.Storage;

/// <summary>No-op implementation used when Backup's Cloudflare R2 settings are not configured.</summary>
public sealed class NullBackupBlobStorage : IBackupBlobStorage
{
    public bool IsConfigured => false;

    public Task<string> UploadAsync(string objectKey, string localFilePath, CancellationToken ct)
        => throw new InvalidOperationException("Backup blob storage is not configured.");

    public Task DownloadAsync(string objectKey, string destinationFilePath, CancellationToken ct)
        => throw new InvalidOperationException("Backup blob storage is not configured.");
}
