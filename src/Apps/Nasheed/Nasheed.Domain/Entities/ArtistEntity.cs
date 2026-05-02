using IhsanDev.Shared.Kernel.Entities;

namespace Nasheed.Domain.Entities;

public class ArtistEntity : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? ImageFileId { get; private set; }
    public int SongCount { get; private set; }

    private ArtistEntity() { }

    public static ArtistEntity Create(string name, string? imageFileId = null)
    {
        return new ArtistEntity
        {
            Name = name,
            ImageFileId = imageFileId,
            SongCount = 0
        };
    }

    public void Update(string? name, string? imageFileId)
    {
        if (name != null) Name = name;
        if (imageFileId != null) ImageFileId = imageFileId;
    }

    public void IncrementSongCount() => SongCount++;
    public void DecrementSongCount() => SongCount = Math.Max(0, SongCount - 1);
}
