using FileManager.Domain.Enums;
using IhsanDev.Shared.Kernel.Entities;

namespace FileManager.Domain.Entities;

public class FileManagerEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Path { get; set; } = string.Empty;
    public FileGroup Group { get; set; }
    public FileType Type { get; set; }
    public bool Temp { get; set; }
    public int? UserId { get; set; }
    public string? ExternalUrl { get; set; }
}
