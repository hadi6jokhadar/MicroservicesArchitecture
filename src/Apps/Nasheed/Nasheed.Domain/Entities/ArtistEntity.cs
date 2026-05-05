using IhsanDev.Shared.Kernel.Entities;

namespace Nasheed.Domain.Entities;

public class ArtistEntity : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public int? ImageFileId { get; private set; }
    public int SongCount { get; private set; }

    private ArtistEntity() { }

    public static ArtistEntity Create(string name, int? imageFileId = null)
    {
        return new ArtistEntity
        {
            Name = name,
            ImageFileId = imageFileId,
            SongCount = 0
        };
    }

    public void Update(string? name, int? imageFileId)
    {
        if (name != null) Name = name;
        if (imageFileId.HasValue) ImageFileId = imageFileId.Value;
    }

    public void IncrementSongCount() => SongCount++;
    public void DecrementSongCount() => SongCount = Math.Max(0, SongCount - 1);
}
