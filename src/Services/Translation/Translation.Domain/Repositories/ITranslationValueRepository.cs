using IhsanDev.Shared.Infrastructure.Persistence;
using Translation.Domain.Entities;

namespace Translation.Domain.Repositories;

/// <summary>
/// Repository interface for translation value operations
/// </summary>
public interface ITranslationValueRepository : IRepository<TranslationValue>
{
    /// <summary>
    /// Get translation values for a specific language
    /// Optionally filter by tenant and category
    /// </summary>
    Task<IEnumerable<TranslationValue>> GetByLanguageAsync(
        string language,
        string? tenantId = null,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get translation value for specific key, language and tenant
    /// </summary>
    Task<TranslationValue?> GetByKeyLanguageTenantAsync(
        int translationKeyId,
        string language,
        string? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all translation values for a specific translation key
    /// </summary>
    Task<IEnumerable<TranslationValue>> GetByKeyIdAsync(
        int translationKeyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete translations for a specific tenant
    /// </summary>
    Task DeleteByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
