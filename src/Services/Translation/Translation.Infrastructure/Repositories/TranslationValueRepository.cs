using Translation.Domain.Entities;
using Translation.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using IhsanDev.Shared.Infrastructure.Persistence;
using Translation.Infrastructure.Persistence;

namespace Translation.Infrastructure.Repositories;

public class TranslationValueRepository : Repository<TranslationValue>, ITranslationValueRepository
{
    public TranslationValueRepository(TranslationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TranslationValue>> GetByLanguageAsync(
        string language,
        string? tenantId = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(v => v.TranslationKey)
            .Where(v => v.Language == language && !v.IsArchived)
            .Where(v => v.TranslationKey.IsActive && !v.TranslationKey.IsArchived);

        // If tenantId is NOT provided: return only global translations (TenantId = null)
        // If tenantId IS provided: return global translations + tenant-specific overrides
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            query = query.Where(v => v.TenantId == null);
        }
        else
        {
            query = query.Where(v => v.TenantId == null || v.TenantId == tenantId);
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(v => v.TranslationKey.Category == category);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<TranslationValue?> GetByKeyLanguageTenantAsync(
        int translationKeyId,
        string language,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.TranslationKeyId == translationKeyId
                    && v.Language == language
                    && v.TenantId == tenantId
                    && !v.IsArchived,
                cancellationToken);
    }

    public async Task<IEnumerable<TranslationValue>> GetByKeyIdAsync(
        int translationKeyId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(v => v.TranslationKeyId == translationKeyId)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var values = await _dbSet
            .Where(v => v.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        foreach (var value in values)
        {
            value.IsArchived = true;
            value.LastModified = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
