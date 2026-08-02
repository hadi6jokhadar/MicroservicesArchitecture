namespace Translation.Application.Interfaces;

/// <summary>
/// Invalidates cached translation responses after a write to <c>TranslationValue</c>.
/// A change to a global value (tenantId is null) falls back into every tenant's already-cached
/// merged response — not just the "global" bucket — so it must clear every tenant-scoped cache
/// entry for that language, not a single key.
/// </summary>
public interface ITranslationCacheInvalidator
{
    Task InvalidateAsync(string language, string? tenantId, string category, CancellationToken cancellationToken = default);
}
