namespace FileManager.Domain.Entities;

/// <summary>
/// Tracks which entities are using a file, preventing premature cleanup of shared files.
/// When a file has 0 usage rows it is marked as Temp=true (eligible for cleanup).
/// When it has 1+ usage rows it is marked as Temp=false (permanent).
/// </summary>
public class FileManagerUsageEntity
{
    public int Id { get; set; }

    /// <summary>Foreign key to FileManager table.</summary>
    public int FileId { get; set; }

    /// <summary>The area/entity type using the file (e.g. "Artist", "Song", "User").</summary>
    public string UsageArea { get; set; } = string.Empty;

    /// <summary>The identifier of the entity using the file (e.g. artist id, user id as string).</summary>
    public string RowId { get; set; } = string.Empty;
}
