namespace Backup.Application.Interfaces;

/// <summary>
/// Abstraction for the offsite/cloud copy of backup files (e.g. Cloudflare R2). Backup owns its
/// own implementation(s) — intentionally not shared with FileManager's <c>IBlobStorage</c>, since
/// Backup has no per-tenant override concept (a single global provider configuration is enough).
/// </summary>
public interface IBackupBlobStorage
{
    /// <summary>Whether a cloud storage provider is configured and available.</summary>
    bool IsConfigured { get; }

    /// <summary>Uploads the file at <paramref name="localFilePath"/> and returns the object key.</summary>
    Task<string> UploadAsync(string objectKey, string localFilePath, CancellationToken ct);

    /// <summary>Downloads the object identified by <paramref name="objectKey"/> to <paramref name="destinationFilePath"/>.</summary>
    Task DownloadAsync(string objectKey, string destinationFilePath, CancellationToken ct);
}
