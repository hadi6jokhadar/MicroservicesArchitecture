using IhsanDev.Shared.Kernel.Entities;

namespace Translation.Domain.Entities;

/// <summary>
/// Represents a translation value for a specific key, language, and optionally tenant
/// TenantId = NULL means it's a global/base translation
/// TenantId = specific value means it's a tenant-specific override
/// </summary>
public class TranslationValue : BaseEntity
{
    public int TranslationKeyId { get; private set; }
    public string Language { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string? TenantId { get; private set; } // NULL = global translation
    
    // Navigation property
    public TranslationKey TranslationKey { get; private set; } = null!;
    
    // EF Core constructor
    private TranslationValue() { }
    
    /// <summary>
    /// Creates a global translation (base translation for all tenants)
    /// </summary>
    public static TranslationValue CreateGlobal(int keyId, string language, string value)
    {
        return new TranslationValue
        {
            TranslationKeyId = keyId,
            Language = language,
            Value = value,
            TenantId = null,
            Created = DateTime.UtcNow,
            Status = true
        };
    }
    
    /// <summary>
    /// Creates a tenant-specific translation override
    /// </summary>
    public static TranslationValue CreateTenantOverride(int keyId, string language, string value, string tenantId)
    {
        return new TranslationValue
        {
            TranslationKeyId = keyId,
            Language = language,
            Value = value,
            TenantId = tenantId,
            Created = DateTime.UtcNow,
            Status = true
        };
    }
    
    public void UpdateValue(string newValue)
    {
        Value = newValue;
        LastModified = DateTime.UtcNow;
    }
}
