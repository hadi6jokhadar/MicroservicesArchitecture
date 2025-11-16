using Microsoft.AspNetCore.Http;

namespace FileManager.Application.Interfaces;

public interface IFileStorage
{
    Task<string> SaveAsync(IFormFile file, string relativePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<Stream> GetAsync(string relativePath, CancellationToken cancellationToken = default);
}
