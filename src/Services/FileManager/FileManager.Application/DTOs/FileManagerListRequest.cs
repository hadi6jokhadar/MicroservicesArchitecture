using FileManager.Domain.Enums;

namespace FileManager.Application.DTOs;

public class FileManagerListRequest
{
    public int? Id { get; set; }
    public bool? Status { get; set; }
    public bool? IsArchived { get; set; } = false;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? TextFilter { get; set; }
    public FileGroup? Group { get; set; }
    public FileType? Type { get; set; }
    public bool? Temp { get; set; }
    public int? UserId { get; set; }
    public string? SortBy { get; set; }
    public bool? Ascending { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
