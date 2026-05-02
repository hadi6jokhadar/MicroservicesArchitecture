using IhsanDev.Shared.Kernel.Entities;

namespace Nasheed.Domain.Entities;

public class PlayLogEntity : BaseEntity
{
    public int SongId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public DateTime PlayedAt { get; private set; }

    // Navigation
    public SongEntity? Song { get; private set; }

    private PlayLogEntity() { }

    public static PlayLogEntity Create(int songId, string userId)
    {
        return new PlayLogEntity
        {
            SongId = songId,
            UserId = userId,
            PlayedAt = DateTime.UtcNow
        };
    }
}
