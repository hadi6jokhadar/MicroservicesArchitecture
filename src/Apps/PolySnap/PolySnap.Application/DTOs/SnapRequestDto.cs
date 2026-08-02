using System.Globalization;
using IhsanDev.Shared.Kernel.Dto.Identity;
using PolySnap.Domain.Entities;

namespace PolySnap.Application.DTOs;

public class SnapRequestDto : BaseDto
{
    public string Name { get; set; } = string.Empty;
    public string RawGeometryGeoJson { get; set; } = string.Empty;
    public string? SnappedGeometryGeoJson { get; set; }
    public double Threshold { get; set; }

    public static SnapRequestDto MapFrom(SnapRequestEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        RawGeometryGeoJson = entity.RawGeometryGeoJson,
        SnappedGeometryGeoJson = entity.SnappedGeometryGeoJson,
        Threshold = entity.Threshold,
        Status = entity.Status,
        IsArchived = entity.IsArchived,
        Created = entity.Created.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        LastModified = entity.LastModified?.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
