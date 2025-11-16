using FileManager.Domain.Entities;
using FileManager.Domain.Enums;
using System.Globalization;

namespace FileManager.Application.DTOs;

public class FileManagerResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Path { get; set; } = string.Empty; // Physical/relative path for storage operations
    public string Url { get; set; } = string.Empty; // Public URL for accessing the file
    public FileGroup Group { get; set; }
    public FileType Type { get; set; }
    public bool Temp { get; set; }
    public bool Status { get; set; }
    public bool IsArchived { get; set; }
    public int? UserId { get; set; }
    public string Created { get; set; } = string.Empty;
    public string? LastModified { get; set; }

    public static FileManagerResponse MapFrom(FileManagerEntity entity, string? rootUrl = null)
    {
        // Normalize path to use forward slashes
        var normalizedPath = entity.Path.Replace("\\", "/");
        
        // Generate public URL
        var publicUrl = string.Empty;
        if (!string.IsNullOrEmpty(rootUrl))
        {
            publicUrl = $"{rootUrl.TrimEnd('/')}/{normalizedPath.TrimStart('/')}";
        }

        return new FileManagerResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Extension = entity.Extension,
            Size = entity.Size,
            Path = normalizedPath, // Storage path (e.g., "ihsandev/system/image/file.webp")
            Url = publicUrl, // Public URL (e.g., "http://localhost:5005/ihsandev/system/image/file.webp")
            Group = entity.Group,
            Type = entity.Type,
            Temp = entity.Temp,
            Status = entity.Status,
            IsArchived = entity.IsArchived,
            UserId = entity.UserId,
            Created = entity.Created.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            LastModified = entity.LastModified?.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
        };
    }
}
