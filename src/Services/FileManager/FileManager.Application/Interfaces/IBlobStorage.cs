using Microsoft.AspNetCore.Http;

namespace FileManager.Application.Interfaces;

/// <summary>
/// Abstraction for third-party blob storage providers (e.g. Cloudflare R2, AWS S3, Azure Blob).
/// Implementations are registered based on the active BlobStorage provider setting.
/// </summary>
public interface IBlobStorage
{
    /// <summary>
    /// Uploads a file stream to the blob provider and returns the public URL.
    /// </summary>
    /// <param name="objectKey">The key/path to store the object under in the bucket.</param>
    /// <param name="stream">File content stream.</param>
    /// <param name="contentType">MIME type of the file.</param>
    /// <param name="contentDisposition">
    /// Optional Content-Disposition to store as object metadata (e.g. "attachment") so the
    /// provider serves it back with that header — used to stop non-raster-image types (SVG in
    /// particular) from rendering as executable markup when the object URL is opened directly.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public URL of the uploaded object.</returns>
    Task<string> UploadAsync(string objectKey, Stream stream, string contentType, string? contentDisposition = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object from the blob provider by its key.
    /// </summary>
    /// <param name="objectKey">The key/path of the object to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the blob storage provider is configured and available.
    /// </summary>
    bool IsConfigured { get; }
}
