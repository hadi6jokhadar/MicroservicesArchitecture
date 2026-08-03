namespace Translation.Application.Interfaces;

/// <summary>
/// Invalidates cached translation responses after a write to <c>TranslationValue</c>.
/// A change to a global value (tenantId is null) falls back into every tenant's already-cached
/// merged response — not just the "global" bucket — so it must clear every tenant-scoped cache
/// entry for that language, not a single key. The "global" bucket itself is always removed via a
/// guaranteed direct delete rather than relying solely on the best-effort, Redis-only pattern
/// flush used for every other tenant's cache.
/// </summary>
public interface ITranslationCacheInvalidator
{
    Task InvalidateAsync(string language, string? tenantId, string category, CancellationToken cancellationToken = default);
}
