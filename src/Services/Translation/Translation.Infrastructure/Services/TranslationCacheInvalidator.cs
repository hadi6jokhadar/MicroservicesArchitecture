using IhsanDev.Shared.Infrastructure.Services.Cache;
using Microsoft.Extensions.Logging;
using Translation.Application.Interfaces;

namespace Translation.Infrastructure.Services;

public class TranslationCacheInvalidator : ITranslationCacheInvalidator
{
    private readonly ICacheService _cache;
    private readonly ILogger<TranslationCacheInvalidator> _logger;

    public TranslationCacheInvalidator(ICacheService cache, ILogger<TranslationCacheInvalidator> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task InvalidateAsync(string language, string? tenantId, string category, CancellationToken cancellationToken = default)
    {
        if (tenantId == null)
        {
            // Global write: every tenant that has no override for the touched key(s) is
            // currently serving this value from its own cached merged response, so the whole
            // language must be flushed, not just translations:{language}:global:*.
            // RemoveByPatternAsync depends on IConnectionMultiplexer (Redis only) — it silently
            // no-ops with a logged warning under the in-memory cache fallback.
            await _cache.RemoveByPatternAsync($"translations:{language}:*", cancellationToken);
            _logger.LogDebug("Invalidated all tenant caches for language {Language} after a global translation change", language);
        }
        else
        {
            await _cache.RemoveAsync($"translations:{language}:{tenantId}:all", cancellationToken);
            await _cache.RemoveAsync($"translations:{language}:{tenantId}:{category}", cancellationToken);
        }
    }
}
