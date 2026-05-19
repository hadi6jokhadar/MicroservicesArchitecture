using System.Globalization;
using IhsanDev.Shared.Kernel.Dto.Identity;
using IhsanDev.Shared.Application.Common.Interfaces;
using Category.Domain.Entities;

namespace Category.Application.DTOs;

public class CategoryDto : BaseDto
{
    public string Slug { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public int? IconFileId { get; set; }
    public int? ImageFileId { get; set; }
    public string? IconName { get; set; }

    /// <summary>Populated at query time via service-to-service call to File Manager.</summary>
    public FileManagerDto? IconFile { get; set; }

    /// <summary>Populated at query time via service-to-service call to File Manager.</summary>
    public FileManagerDto? ImageFile { get; set; }

    public int? ParentId { get; set; }
    public string Path { get; set; } = "/";
    public int Depth { get; set; }

    /// <summary>Locale → display name mapping.</summary>
    public Dictionary<string, string> NameTranslations { get; set; } = new();

    /// <summary>Schema-less JSONB attributes.</summary>
    public Dictionary<string, object> Attributes { get; set; } = new();

    /// <summary>Child nodes — populated only for full-tree responses.</summary>
    public List<CategoryDto> Children { get; set; } = new();

    public static CategoryDto MapFrom(CategoryEntity entity) => new()
    {
        Id = entity.Id,
        Slug = entity.Slug,
        Uri = entity.Uri,
        IconFileId = entity.IconFileId,
        ImageFileId = entity.ImageFileId,
        IconName = entity.IconName,
        ParentId = entity.ParentId,
        Path = entity.Path,
        Depth = entity.Depth,
        NameTranslations = entity.NameTranslations.Translations.ToDictionary(k => k.Key, v => v.Value),
        Attributes = entity.Attributes,
        IsArchived = entity.IsArchived,
        Status = entity.Status,
        Created = entity.Created.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        LastModified = entity.LastModified?.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };

    /// <summary>Builds a recursive tree from a flat list, using ParentId relationships.</summary>
    public static List<CategoryDto> BuildTree(List<CategoryEntity> flat)
    {
        var dtos = flat.Select(MapFrom).ToDictionary(d => d.Id);

        var roots = new List<CategoryDto>();
        foreach (var dto in dtos.Values)
        {
            if (dto.ParentId == null)
                roots.Add(dto);
            else if (dtos.TryGetValue(dto.ParentId.Value, out var parent))
                parent.Children.Add(dto);
        }
        return roots;
    }

    /// <summary>Builds a recursive tree from an already-mapped flat list of DTOs.</summary>
    public static List<CategoryDto> BuildTree(List<CategoryDto> flatDtos)
    {
        var dict = flatDtos.ToDictionary(d => d.Id);

        var roots = new List<CategoryDto>();
        foreach (var dto in dict.Values)
        {
            if (dto.ParentId == null)
                roots.Add(dto);
            else if (dict.TryGetValue(dto.ParentId.Value, out var parent))
                parent.Children.Add(dto);
        }
        return roots;
    }
}
