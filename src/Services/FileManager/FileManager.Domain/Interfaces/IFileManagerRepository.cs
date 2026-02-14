using FileManager.Domain.Entities;
using FileManager.Domain.Enums;
using IhsanDev.Shared.Infrastructure.Persistence;

namespace FileManager.Domain.Interfaces;

public interface IFileManagerRepository : IRepository<FileManagerEntity>
{
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
    Task<int> DeleteAllTempAsync(CancellationToken cancellationToken = default);
    Task<int> DeleteOldTempFilesAsync(int olderThanDays, CancellationToken cancellationToken = default);
    Task<List<FileManagerEntity>> GetTempFilesAsync(CancellationToken cancellationToken = default);
    Task<List<FileManagerEntity>> GetOldTempFilesAsync(int olderThanDays, CancellationToken cancellationToken = default);
}
