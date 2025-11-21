using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Domain.Entities;
using FileManager.Domain.Enums;
using FileManager.Domain.Exceptions;
using FileManager.Domain.Interfaces;
using FileManager.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileManager.Infrastructure.Services;

public class FileManagerService : IFileManagerService
{
    private readonly IFileManagerRepository _repository;
    private readonly IFileStorage _fileStorage;
    private readonly FileManagerOptions _options;
    private readonly ILogger<FileManagerService> _logger;
    private readonly string _urlPrefix;

    public FileManagerService(
        IFileManagerRepository repository,
        IFileStorage fileStorage,
        IOptions<FileManagerOptions> options,
        ILogger<FileManagerService> logger)
    {
        _repository = repository;
        _fileStorage = fileStorage;
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
            throw new FileValidationException("File is empty or null.");
        }

        if (file.Length > _options.MaxFileSizeBytes)
        {
            throw new FileValidationException(
                $"File size ({file.Length} bytes) exceeds maximum allowed size ({_options.MaxFileSizeBytes} bytes).");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension))
        {
            throw new FileValidationException(
                $"File extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", _options.AllowedExtensions)}");
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
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
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

    public async Task<bool> DeleteFileAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("File with ID {Id} not found for deletion", id);
            return false;
        }

        // Delete from storage
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

    public async Task<int> DeleteOldTempFilesAsync(int olderThanDays, CancellationToken cancellationToken = default)
    {
        var oldTempFiles = await _repository.GetOldTempFilesAsync(olderThanDays, cancellationToken);

        foreach (var file in oldTempFiles)
        {
            try
            {
                await _fileStorage.DeleteAsync(file.Path, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old temp file from storage: {Path}", file.Path);
            }
        }

        var deletedCount = await _repository.DeleteOldTempFilesAsync(olderThanDays, cancellationToken);

        _logger.LogInformation("Deleted {Count} old temporary files (older than {Days} days)", deletedCount, olderThanDays);

        return deletedCount;
    }

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
