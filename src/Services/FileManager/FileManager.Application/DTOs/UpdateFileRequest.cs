using FileManager.Domain.Enums;

namespace FileManager.Application.DTOs;

public record UpdateFileRequest(
    string? Name = null,
    FileGroup? Group = null,
    bool? Status = null,
    bool? IsArchived = null,
    bool? Temp = null
);
