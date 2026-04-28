using FileManager.Application.DTOs;
using FileManager.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace FileManager.Application.Interfaces;

public interface IFileManagerService
{
    Task<FileManagerResponse> SaveFileAsync(IFormFile file, FileGroup group, int? userId = null, CancellationToken cancellationToken = default);
    Task<FileManagerResponse?> GetFileByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<FileManagerResponse>> GetFilesByIdsAsync(List<int> ids, CancellationToken cancellationToken = default);
    Task<PaginatedList<FileManagerResponse>> GetFilesAsync(FileManagerListRequest request, CancellationToken cancellationToken = default);
    Task<FileManagerResponse> UpdateFileAsync(int id, string? name = null, FileGroup? group = null, bool? status = null, bool? isArchived = null, bool? temp = null, CancellationToken cancellationToken = default);
    Task<FileManagerResponse> ToggleArchiveStatusAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(int id, CancellationToken cancellationToken = default);
    Task<int> DeleteAllTempFilesAsync(CancellationToken cancellationToken = default);
    Task<int> DeleteOldTempFilesAsync(int olderThanDays, int aiOlderThanDays = 30, CancellationToken cancellationToken = default);
    Task<FileManagerResponse> UploadToBlobAsync(int id, CancellationToken cancellationToken = default);
    Task<FileManagerResponse> RemoveFromBlobAsync(int id, CancellationToken cancellationToken = default);
}
