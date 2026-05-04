using FileManager.Domain.Entities;

namespace FileManager.Domain.Interfaces;

public interface IFileManagerUsageRepository
{
    Task<FileManagerUsageEntity?> GetUsageAsync(int fileId, string usageArea, string rowId, CancellationToken cancellationToken = default);
    Task<int> CountUsagesAsync(int fileId, CancellationToken cancellationToken = default);
    Task AddAsync(FileManagerUsageEntity usage, CancellationToken cancellationToken = default);
    Task RemoveAsync(FileManagerUsageEntity usage, CancellationToken cancellationToken = default);
}
