using IhsanDev.Shared.Kernel.Dto.Identity;
using Translation.Domain.Entities;

namespace Translation.Application.DTOs;

public class TranslationKeyDto : BaseDto
{
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<TranslationValueDto> Values { get; set; } = new();

    public static TranslationKeyDto MapFrom(TranslationKey key)
    {
        return new TranslationKeyDto
        {
            Id = key.Id,
            Key = key.Key,
            Category = key.Category,
            Description = key.Description,
            IsActive = key.IsActive,
            Status = key.Status,
            IsArchived = key.IsArchived,
            Created = key.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            LastModified = key.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            Values = key.Values
                .Where(v => !v.IsArchived)
                .Select(v => TranslationValueDto.MapFrom(v, key.Key))
                .ToList()
        };
    }
}
