using Nasheed.Domain.Entities;

namespace Nasheed.Application.DTOs;

public class FavoriteDto
{
    public int UserId { get; set; }
    public int SongId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;

    public static FavoriteDto MapFrom(FavoriteEntity entity) => new()
    {
        UserId = entity.UserId,
        SongId = entity.SongId,
        CreatedAt = entity.CreatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)
    };
}
