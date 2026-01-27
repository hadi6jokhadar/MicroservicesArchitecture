using IhsanDev.Shared.Kernel.Entities;

namespace Translation.Domain.Entities;

/// <summary>
/// Represents a translation key that can have multiple translations across different languages and tenants
/// </summary>
public class TranslationKey : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    
    // Navigation property
    public ICollection<TranslationValue> Values { get; private set; } = new List<TranslationValue>();
    
    // EF Core constructor
    private TranslationKey() { }
    
    /// <summary>
    /// Creates a new translation key
    /// </summary>
    public static TranslationKey Create(string key, string category, string? description = null)
    {
        return new TranslationKey
        {
            Key = key,
            Category = category,
            Description = description,
            IsActive = true,
            Created = DateTime.UtcNow,
            Status = true
        };
    }
    
    public void Update(string? description = null)
    {
        if (description != null)
        {
            Description = description;
        }
        LastModified = DateTime.UtcNow;
    }
    
    public void Deactivate()
    {
        IsActive = false;
        LastModified = DateTime.UtcNow;
    }
    
    public void Activate()
    {
        IsActive = true;
        LastModified = DateTime.UtcNow;
    }
}
