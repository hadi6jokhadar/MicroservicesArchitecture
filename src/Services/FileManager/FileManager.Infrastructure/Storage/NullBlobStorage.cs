using FileManager.Application.Interfaces;

namespace FileManager.Infrastructure.Storage;

/// <summary>
/// No-op implementation used when blob storage is not configured.
/// </summary>
public sealed class NullBlobStorage : IBlobStorage
{
    public bool IsConfigured => false;

    public Task<string> UploadAsync(string objectKey, Stream stream, string contentType, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Blob storage is not configured for this tenant.");

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        => Task.CompletedTask; // Silently skip — nothing to delete if provider was never set
}
