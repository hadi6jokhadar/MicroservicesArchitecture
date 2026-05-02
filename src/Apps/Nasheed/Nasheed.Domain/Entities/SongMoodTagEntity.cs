using IhsanDev.Shared.Kernel.Entities;

namespace Nasheed.Domain.Entities;

public class SongMoodTagEntity : BaseEntity
{
    public int SongId { get; private set; }
    public string Tag { get; private set; } = string.Empty;

    // Navigation
    public SongEntity? Song { get; private set; }

    private SongMoodTagEntity() { }

    public static SongMoodTagEntity Create(int songId, string tag)
    {
        return new SongMoodTagEntity
        {
            SongId = songId,
            Tag = tag
        };
    }
}
