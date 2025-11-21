using FileManager.Domain.Entities;
using FileManager.Domain.Enums;

namespace FileManager.Domain.Interfaces;

public interface IFileManagerRepository
{
    Task<FileManagerEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<FileManagerEntity>> GetByIdsAsync(List<int> ids, CancellationToken cancellationToken = default);
    Task<(List<FileManagerEntity> Items, int TotalCount)> GetAllAsync(
        int? id = null,
        bool? status = null,
        bool? isArchived = null,
        DateTime? from = null,
        DateTime? to = null,
        string? textFilter = null,
        FileGroup? group = null,
        FileType? type = null,
        bool? temp = null,
        int? userId = null,
        string sortBy = "Id",
        bool ascending = true,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
    Task<FileManagerEntity> AddAsync(FileManagerEntity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(FileManagerEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(FileManagerEntity entity, CancellationToken cancellationToken = default);
    Task<int> DeleteAllTempAsync(CancellationToken cancellationToken = default);
    Task<int> DeleteOldTempFilesAsync(int olderThanDays, CancellationToken cancellationToken = default);
    Task<List<FileManagerEntity>> GetTempFilesAsync(CancellationToken cancellationToken = default);
    Task<List<FileManagerEntity>> GetOldTempFilesAsync(int olderThanDays, CancellationToken cancellationToken = default);
}
