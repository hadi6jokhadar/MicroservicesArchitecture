using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Domain.Entities;
using FileManager.Domain.Enums;
using FileManager.Domain.Interfaces;
using FileManager.Infrastructure.Options;
using FileManager.Infrastructure.Storage;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileManager.Infrastructure.Services;

public class FileManagerService : IFileManagerService
{
    private readonly IFileManagerRepository _repository;
    private readonly IFileManagerUsageRepository _usageRepository;
    private readonly IFileStorage _fileStorage;
    private readonly BlobStorageFactory _blobStorageFactory;
    private readonly FileManagerOptions _options;
    private readonly ILogger<FileManagerService> _logger;
    private readonly string _urlPrefix;

    public FileManagerService(
        IFileManagerRepository repository,
        IFileManagerUsageRepository usageRepository,
        IFileStorage fileStorage,
        BlobStorageFactory blobStorageFactory,
        IOptions<FileManagerOptions> options,
        ILogger<FileManagerService> logger)
    {
        _repository = repository;
        _usageRepository = usageRepository;
        _fileStorage = fileStorage;
        _blobStorageFactory = blobStorageFactory;
        _options = options.Value;
        _logger = logger;
        // RootStoragePath is the URL prefix for responses
        _urlPrefix = _options.RootStoragePath?.TrimEnd('/') ?? string.Empty;
    }

    public async Task<FileManagerResponse> SaveFileAsync(
        IFormFile file,
        FileGroup group,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        // Validate file
        if (file == null || file.Length == 0)
        {
            throw new BadRequestException(LocalizationKeys.Exceptions.FileEmpty);
        }

        if (file.Length > _options.MaxFileSizeBytes)
        {
            throw new BadRequestException(LocalizationKeys.Exceptions.FileSizeExceeded);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension))
        {
            throw new BadRequestException(LocalizationKeys.Exceptions.InvalidFileType);
        }

        // Map extension to FileType
        var fileType = MapExtensionToFileType(extension);

        // Generate unique filename
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";

        // Build path: {userId}/{category}/{filename} or system/{category}/{filename}
        var category = fileType.ToString().ToLowerInvariant();
        var relativePath = userId.HasValue
            ? Path.Combine(userId.Value.ToString(), category, uniqueFileName)
            : Path.Combine("system", category, uniqueFileName);

        // Save file to storage
        var savedPath = await _fileStorage.SaveAsync(file, relativePath, cancellationToken);

        // Create entity
        var entity = new FileManagerEntity
        {
            Name = Path.GetFileNameWithoutExtension(file.FileName),
            Extension = extension,
            Size = file.Length,
            Path = savedPath,
            Group = group,
            Type = fileType,
            Temp = true,
            Status = true,
            IsArchived = false,
            UserId = userId,
            Created = DateTime.UtcNow
        };

        var savedEntity = await _repository.AddAsync(entity, cancellationToken);

        _logger.LogInformation("File saved successfully: ID={Id}, Name={Name}, Path={Path}",
            savedEntity.Id, savedEntity.Name, savedEntity.Path);

        return FileManagerResponse.MapFrom(savedEntity, _urlPrefix);
    }

    public async Task<FileManagerResponse?> GetFileByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        return entity != null ? FileManagerResponse.MapFrom(entity, _urlPrefix) : null;
    }

    public async Task<List<FileManagerResponse>> GetFilesByIdsAsync(List<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || !ids.Any())
        {
            return new List<FileManagerResponse>();
        }

        var entities = await _repository.GetByIdsAsync(ids, cancellationToken);
        return entities.Select(e => FileManagerResponse.MapFrom(e, _urlPrefix)).ToList();
    }

    public async Task<PaginatedList<FileManagerResponse>> GetFilesAsync(
        FileManagerListRequest request,
        CancellationToken cancellationToken = default)
    {
        // Apply default values if not provided
        var sortBy = request.SortBy ?? "Id";
        var ascending = request.Ascending ?? true;

        var (items, totalCount) = await _repository.GetAllAsync(
            id: request.Id,
            status: request.Status,
            isArchived: request.IsArchived,
            from: request.From,
            to: request.To,
            textFilter: request.TextFilter,
            group: request.Group,
            type: request.Type,
            temp: request.Temp,
            userId: request.UserId,
            sortBy: sortBy,
            ascending: ascending,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return new PaginatedList<FileManagerResponse>
        {
            Items = items.Select(e => FileManagerResponse.MapFrom(e, _urlPrefix)).ToList(),
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<FileManagerResponse> UpdateFileAsync(
        int id,
        string? name = null,
        FileGroup? group = null,
        bool? status = null,
        bool? isArchived = null,
        bool? temp = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithArchivedAsync(id, cancellationToken);
        if (entity == null)
        {
            throw new Domain.Exceptions.FileNotFoundException(id);
        }

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(name))
            entity.Name = name;

        if (group.HasValue)
            entity.Group = group.Value;

        if (status.HasValue)
            entity.Status = status.Value;

        if (isArchived.HasValue)
            entity.IsArchived = isArchived.Value;

        if (temp.HasValue)
            entity.Temp = temp.Value;

        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("File updated successfully: ID={Id}", id);

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    public async Task<FileManagerResponse?> UpdateFileTempStatusAsync(
        int fileId,
        string usageArea,
        string rowId,
        bool isNew,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(fileId, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("File with ID {FileId} not found for temp status update", fileId);
            return null;
        }

        var existingUsage = await _usageRepository.GetUsageAsync(fileId, usageArea, rowId, cancellationToken);

        if (isNew)
        {
            // Add usage row if it doesn't already exist
            if (existingUsage == null)
            {
                var usage = new FileManagerUsageEntity
                {
                    FileId = fileId,
                    UsageArea = usageArea,
                    RowId = rowId
                };
                await _usageRepository.AddAsync(usage, cancellationToken);
                _logger.LogInformation("Added usage row for FileId={FileId} UsageArea={UsageArea} RowId={RowId}", fileId, usageArea, rowId);
            }
        }
        else
        {
            // Remove usage row if it exists
            if (existingUsage != null)
            {
                await _usageRepository.RemoveAsync(existingUsage, cancellationToken);
                _logger.LogInformation("Removed usage row for FileId={FileId} UsageArea={UsageArea} RowId={RowId}", fileId, usageArea, rowId);
            }
        }

        // Recalculate temp status based on remaining usages
        var usageCount = await _usageRepository.CountUsagesAsync(fileId, cancellationToken);
        entity.Temp = usageCount == 0;

        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("File temp status updated: ID={FileId} Temp={Temp} (usages={Count})", fileId, entity.Temp, usageCount);

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    public async Task<FileManagerResponse> ToggleArchiveStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithArchivedAsync(id, cancellationToken);
        if (entity == null)
        {
            throw new Domain.Exceptions.FileNotFoundException(id);
        }

        entity.IsArchived = !entity.IsArchived;
        entity.LastModified = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        
        _logger.LogInformation("File archive status toggled: ID={Id}, IsArchived={IsArchived}", id, entity.IsArchived);

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    public async Task<bool> DeleteFileAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("File with ID {Id} not found for deletion", id);
            return false;
        }

        // Delete from blob storage if an external URL exists
        if (!string.IsNullOrWhiteSpace(entity.ExternalUrl))
        {
            try
            {
                var objectKey = ExtractObjectKeyFromExternalUrl(entity.ExternalUrl);
                var blob = _blobStorageFactory.Create();
                if (blob.IsConfigured)
                {
                    await blob.DeleteAsync(objectKey, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file from blob storage. FileId: {Id}, ExternalUrl: {Url}", id, entity.ExternalUrl);
            }
        }

        // Delete from local storage
        await _fileStorage.DeleteAsync(entity.Path, cancellationToken);

        // Delete from database
        await _repository.DeleteAsync(entity, cancellationToken);

        _logger.LogInformation("File deleted successfully: ID={Id}, Path={Path}", id, entity.Path);

        return true;
    }

    public async Task<int> DeleteAllTempFilesAsync(CancellationToken cancellationToken = default)
    {
        var tempFiles = await _repository.GetTempFilesAsync(cancellationToken);

        foreach (var file in tempFiles)
        {
            try
            {
                await DeleteBlobIfPresentAsync(file, cancellationToken);
                await _fileStorage.DeleteAsync(file.Path, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file from storage: {Path}", file.Path);
            }
        }

        var deletedCount = await _repository.DeleteAllTempAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} temporary files", deletedCount);

        return deletedCount;
    }

    public async Task<int> DeleteOldTempFilesAsync(int olderThanDays, int aiOlderThanDays = 30, CancellationToken cancellationToken = default)
    {
        var oldTempFiles = await _repository.GetOldTempFilesAsync(olderThanDays, aiOlderThanDays, cancellationToken);

        foreach (var file in oldTempFiles)
        {
            try
            {
                await DeleteBlobIfPresentAsync(file, cancellationToken);
                await _fileStorage.DeleteAsync(file.Path, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old temp file from storage: {Path}", file.Path);
            }
        }

        var deletedCount = await _repository.DeleteOldTempFilesAsync(olderThanDays, aiOlderThanDays, cancellationToken);

        _logger.LogInformation("Deleted {Count} old temporary files (older than {Days} days, AI files older than {AiDays} days)", deletedCount, olderThanDays, aiOlderThanDays);

        return deletedCount;
    }

    public async Task<FileManagerResponse> UploadToBlobAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithArchivedAsync(id, cancellationToken);
        if (entity == null)
        {
            throw new Domain.Exceptions.FileNotFoundException(id);
        }

        var blob = _blobStorageFactory.Create();
        if (!blob.IsConfigured)
        {
            throw new InvalidOperationException("Blob storage is not configured. Set BlobStorage settings in appsettings.json or tenant configuration.");
        }

        // Read the local file from storage
        var stream = await _fileStorage.GetAsync(entity.Path, cancellationToken);
        var contentType = GetContentType(entity.Extension);
        var objectKey = entity.Path.Replace("\\", "/");

        var publicUrl = await blob.UploadAsync(objectKey, stream, contentType, cancellationToken);

        entity.ExternalUrl = publicUrl;
        entity.LastModified = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("File {Id} uploaded to blob. ExternalUrl: {Url}", id, publicUrl);

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    public async Task<FileManagerResponse> RemoveFromBlobAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithArchivedAsync(id, cancellationToken);
        if (entity == null)
        {
            throw new Domain.Exceptions.FileNotFoundException(id);
        }

        if (!string.IsNullOrWhiteSpace(entity.ExternalUrl))
        {
            var blob = _blobStorageFactory.Create();
            if (blob.IsConfigured)
            {
                var objectKey = ExtractObjectKeyFromExternalUrl(entity.ExternalUrl);
                await blob.DeleteAsync(objectKey, cancellationToken);
            }

            entity.ExternalUrl = null;
            entity.LastModified = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, cancellationToken);

            _logger.LogInformation("File {Id} removed from blob storage.", id);
        }

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    /// <summary>Deletes from blob storage if the entity has an ExternalUrl set.</summary>
    private async Task DeleteBlobIfPresentAsync(FileManagerEntity entity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entity.ExternalUrl))
            return;

        try
        {
            var blob = _blobStorageFactory.Create();
            if (blob.IsConfigured)
            {
                var objectKey = ExtractObjectKeyFromExternalUrl(entity.ExternalUrl);
                await blob.DeleteAsync(objectKey, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove blob for file {Id}, ExternalUrl: {Url}", entity.Id, entity.ExternalUrl);
        }
    }

    /// <summary>
    /// Extracts the object key from a full public URL.
    /// e.g. https://pub-xxx.r2.dev/tenant/123/image/file.jpg → tenant/123/image/file.jpg
    /// </summary>
    private static string ExtractObjectKeyFromExternalUrl(string externalUrl)
    {
        var uri = new Uri(externalUrl);
        // Remove leading slash from path
        return uri.AbsolutePath.TrimStart('/');
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".svg" => "image/svg+xml",
        ".mp4" => "video/mp4",
        ".avi" => "video/x-msvideo",
        ".mkv" => "video/x-matroska",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".ogg" => "audio/ogg",
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };

    private FileType MapExtensionToFileType(string extension)
    {
        if (_options.ExtensionToTypeMapping.TryGetValue(extension, out var typeName))
        {
            if (Enum.TryParse<FileType>(typeName, true, out var fileType))
            {
                return fileType;
            }
        }

        // Default mapping if not found in configuration
        return extension switch
        {
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" => FileType.Music,
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => FileType.Video,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" or ".webp" => FileType.Image,
            _ => FileType.Other
        };
    }
}
