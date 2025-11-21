namespace IhsanDev.Shared.Application.Common.Interfaces;

/// <summary>
/// Client interface for communicating with FileManager Service
/// Provides fast service-to-service file operations
/// </summary>
public interface IFileManagerServiceClient
{
    /// <summary>
    /// Get file metadata by ID from FileManager service
    /// Uses internal endpoint for fast service-to-service communication
    /// </summary>
    /// <param name="fileId">The ID of the file to retrieve</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenant files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File metadata or null if not found or on error</returns>
    Task<FileManagerDto?> GetFileByIdAsync(int fileId, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get multiple files by IDs in a single batch request
    /// Efficiently fetches multiple files to prevent N+1 query problems
    /// </summary>
    /// <param name="fileIds">List of file IDs to retrieve</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenant files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping file IDs to file metadata (missing files excluded)</returns>
    Task<Dictionary<int, FileManagerDto>> GetFilesByIdsAsync(IEnumerable<int> fileIds, string? tenantId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO representing file metadata from FileManager service
/// </summary>
public class FileManagerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Group { get; set; }
    public int Type { get; set; }
    public bool Temp { get; set; }
    public bool Status { get; set; }
    public bool IsArchived { get; set; }
    public int? UserId { get; set; }
    public string Created { get; set; } = string.Empty;
    public string? LastModified { get; set; }
}
