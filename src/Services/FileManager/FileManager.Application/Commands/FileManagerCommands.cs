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

public record DeleteAllTempFilesCommand() : IRequest<int>;

public record DeleteOldTempFilesCommand(int OlderThanDays) : IRequest<int>;

public record ToggleArchiveFileCommand(int Id) : IRequest<FileManagerResponse>;