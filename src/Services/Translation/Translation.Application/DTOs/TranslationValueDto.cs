using Translation.Domain.Entities;

namespace Translation.Application.DTOs;

public class TranslationValueDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? TenantId { get; set; }

    public static TranslationValueDto MapFrom(TranslationValue value, string key)
    {
        return new TranslationValueDto
        {
            Id = value.Id,
            Key = key,
            Language = value.Language,
            Value = value.Value,
            TenantId = value.TenantId
        };
    }
}
