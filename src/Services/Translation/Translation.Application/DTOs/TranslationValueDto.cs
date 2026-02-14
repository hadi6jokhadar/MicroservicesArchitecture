using Translation.Domain.Entities;

namespace Translation.Application.DTOs;

public class TranslationValueDto
{
    public int Id { get; set; }
    public int TranslationKeyId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string Created { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
    public string? LastModified { get; set; }

    public static TranslationValueDto MapFrom(TranslationValue value, string key)
    {
        return new TranslationValueDto
        {
            Id = value.Id,
            TranslationKeyId = value.TranslationKeyId,
            Key = key,
            Language = value.Language,
            Value = value.Value,
            TenantId = value.TenantId,
            Created = value.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            LastModified = value.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
