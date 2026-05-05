using System.Globalization;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Dto.Identity;
using Nasheed.Domain.Entities;

namespace Nasheed.Application.DTOs;

public class ArtistDto : BaseDto
{
    public string Name { get; set; } = string.Empty;
    public int? ImageFileId { get; set; }
    public FileManagerDto? ImageFile { get; set; }
    public int SongCount { get; set; }

    public static ArtistDto MapFrom(ArtistEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ImageFileId = entity.ImageFileId,
        ImageFile = null, // Populated by handler via FileManager service
        SongCount = entity.SongCount,
        Status = entity.Status,
        IsArchived = entity.IsArchived,
        Created = entity.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        LastModified = entity.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
