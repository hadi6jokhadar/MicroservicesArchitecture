using Translation.Domain.Entities;

namespace Translation.Application.DTOs;

public class TranslationKeyDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string Created { get; set; } = string.Empty;
    public string? LastModified { get; set; }
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
            Created = key.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            LastModified = key.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            Values = key.Values
                .Where(v => !v.IsArchived)
                .Select(v => new TranslationValueDto
                {
                    Id = v.Id,
                    Key = key.Key,
                    Language = v.Language,
                    Value = v.Value,
                    TenantId = v.TenantId
                })
                .ToList()
        };
    }
}
