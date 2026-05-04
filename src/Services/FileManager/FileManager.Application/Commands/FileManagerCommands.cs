using FileManager.Application.DTOs;
using FileManager.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace FileManager.Application.Commands;

public record SaveFileCommand(
    IFormFile File,
    FileGroup Group,
    int? UserId = null
) : IRequest<FileManagerResponse>;

public record UpdateFileCommand(
    int Id,
    string? Name = null,
    FileGroup? Group = null,
    bool? Status = null,
    bool? IsArchived = null,
    bool? Temp = null
) : IRequest<FileManagerResponse>;

public record DeleteFileCommand(int Id) : IRequest<bool>;

public record UpdateFileTempStatusCommand(
    int FileId,
    string UsageArea,
    string RowId,
    bool IsNew
) : IRequest<FileManagerResponse?>;

public record DeleteAllTempFilesCommand() : IRequest<int>;

public record DeleteOldTempFilesCommand(int OlderThanDays, int AiOlderThanDays = 30) : IRequest<int>;

public record ToggleArchiveFileCommand(int Id) : IRequest<FileManagerResponse>;

/// <summary>Uploads the local file to the configured blob provider and stores the public URL in ExternalUrl.</summary>
public record UploadToBlobCommand(int FileId) : IRequest<FileManagerResponse>;

/// <summary>Removes the file from the blob provider and clears ExternalUrl.</summary>
public record RemoveFromBlobCommand(int FileId) : IRequest<FileManagerResponse>;