namespace Nasheed.Domain.Entities;

/// <summary>
/// Join table — does not inherit BaseEntity (no audit fields needed for a pure relation).
/// Composite PK: UserId + SongId.
/// </summary>
public class FavoriteEntity
{
    public string UserId { get; private set; } = string.Empty;
    public int SongId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public SongEntity? Song { get; private set; }

    private FavoriteEntity() { }

    public static FavoriteEntity Create(string userId, int songId)
    {
        return new FavoriteEntity
        {
            UserId = userId,
            SongId = songId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
