using FileManager.Application.Interfaces;
using FileManager.Domain.Exceptions;
using FileManager.Infrastructure.Options;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace FileManager.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly FileManagerOptions _options;
    private readonly ILogger<LocalFileStorage> _logger;
    private readonly ITenantContext? _tenantContext;

    public LocalFileStorage(
        IOptions<FileManagerOptions> options,
        ILogger<LocalFileStorage> logger,
        ITenantContext? tenantContext = null)
    {
        _options = options.Value;
        _logger = logger;
        _tenantContext = tenantContext;
    }

    public async Task<string> SaveAsync(IFormFile file, string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetFullPath(relativePath);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            // Return path including tenant ID for URL construction
            var pathWithTenant = GetRelativePathWithTenant(relativePath);
            _logger.LogInformation("File saved successfully. Physical: {FullPath}, Stored: {StoredPath}", fullPath, pathWithTenant);
            return pathWithTenant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file: {Path}", relativePath);
            throw new FileStorageException($"Failed to save file: {relativePath}", ex);
        }
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // The relativePath stored in DB includes tenant folder (e.g., "ihsandev/system/image/file.webp")
            // We need to construct the full physical path directly from storage root
            var fullPath = Path.Combine(_options.FilesSavePath, relativePath.Replace("/", "\\"));

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("File deleted successfully: {FullPath}", fullPath);
            }
            else
            {
                _logger.LogWarning("File not found for deletion: {FullPath}", fullPath);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {Path}", relativePath);
            throw new FileStorageException($"Failed to delete file: {relativePath}", ex);
        }
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        // The relativePath stored in DB includes tenant folder (e.g., "ihsandev/system/image/file.webp")
        var fullPath = Path.Combine(_options.FilesSavePath, relativePath.Replace("/", "\\"));
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<Stream> GetAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // The relativePath stored in DB includes tenant folder (e.g., "ihsandev/system/image/file.webp")
            var fullPath = Path.Combine(_options.FilesSavePath, relativePath.Replace("/", "\\"));

            if (!File.Exists(fullPath))
            {
                throw new FileStorageException($"File not found: {relativePath}");
            }

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            return Task.FromResult<Stream>(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {Path}", relativePath);
            throw new FileStorageException($"Failed to read file: {relativePath}", ex);
        }
    }

    private string GetFullPath(string relativePath)
    {
        // Get tenant-specific or default root path
        // FilesSavePath is the physical storage location on disk
        string rootPath;

        if (_tenantContext != null && !string.IsNullOrEmpty(_tenantContext.TenantId))
        {
            // Sanitize tenant ID for folder name (remove special characters)
            var sanitizedTenantId = SanitizeTenantId(_tenantContext.TenantId);
            rootPath = Path.Combine(_options.FilesSavePath, sanitizedTenantId);
        }
        else
        {
            rootPath = Path.Combine(_options.FilesSavePath, "default");
        }

        return Path.Combine(rootPath, relativePath);
    }

    private string GetRelativePathWithTenant(string relativePath)
    {
        // Normalize path to forward slashes first
        var normalizedPath = relativePath.Replace("\\", "/");
        
        // Return path including tenant folder for URL construction
        if (_tenantContext != null && !string.IsNullOrEmpty(_tenantContext.TenantId))
        {
            var sanitizedTenantId = SanitizeTenantId(_tenantContext.TenantId);
            var result = $"{sanitizedTenantId}/{normalizedPath}";
            _logger.LogDebug("Path with tenant - TenantId: {TenantId}, Result: {Result}", _tenantContext.TenantId, result);
            return result;
        }
        
        var defaultResult = $"default/{normalizedPath}";
        _logger.LogWarning("No tenant context found, using default path: {Path}", defaultResult);
        return defaultResult;
    }

    private static string SanitizeTenantId(string tenantId)
    {
        // Remove special characters and replace spaces/dashes with underscores
        return Regex.Replace(tenantId, @"[^a-zA-Z0-9-]", "");
    }
}
