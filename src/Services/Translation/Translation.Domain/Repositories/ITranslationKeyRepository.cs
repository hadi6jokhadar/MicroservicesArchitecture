using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Application.Common.Models;
using Translation.Domain.Entities;

namespace Translation.Domain.Repositories;

/// <summary>
/// Repository interface for translation key operations
/// </summary>
public interface ITranslationKeyRepository : IRepository<TranslationKey>
{
    /// <summary>
    /// Get translation key by key string
    /// </summary>
    Task<TranslationKey?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get translation keys by category
    /// </summary>
    Task<IEnumerable<TranslationKey>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if key exists
    /// </summary>
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paginated translation keys with optional filtering
    /// </summary>
    Task<PaginatedList<TranslationKey>> GetPaginatedAsync(
        int pageNumber = 1,
        int pageSize = 10,
        string? category = null,
        string? searchTerm = null,
        bool isArchived = false,
        CancellationToken cancellationToken = default);
}
