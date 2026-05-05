using Nasheed.Domain.Entities;

namespace Nasheed.Application.DTOs;

public class RatingDto
{
    public int UserId { get; set; }
    public int SongId { get; set; }
    public int Value { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? UpdatedAt { get; set; }

    public static RatingDto MapFrom(RatingEntity entity) => new()
    {
        UserId = entity.UserId,
        SongId = entity.SongId,
        Value = entity.Value,
        CreatedAt = entity.CreatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
        UpdatedAt = entity.UpdatedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)
    };
}
