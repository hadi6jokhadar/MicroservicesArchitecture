namespace Translation.Application.DTOs;

public class TranslationsDto
{
    public Dictionary<string, string> Translations { get; set; } = new();
    public string Language { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string CachedAt { get; set; } = string.Empty;
}
