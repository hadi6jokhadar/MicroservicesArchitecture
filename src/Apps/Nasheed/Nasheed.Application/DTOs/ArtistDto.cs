using System.Globalization;
using IhsanDev.Shared.Kernel.Dto.Identity;
using Nasheed.Domain.Entities;

namespace Nasheed.Application.DTOs;

public class ArtistDto : BaseDto
{
    public string Name { get; set; } = string.Empty;
    public string? ImageFileId { get; set; }
    public int SongCount { get; set; }

    public static ArtistDto MapFrom(ArtistEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ImageFileId = entity.ImageFileId,
        SongCount = entity.SongCount,
        Status = entity.Status,
        IsArchived = entity.IsArchived,
        Created = entity.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        LastModified = entity.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
