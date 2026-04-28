using FileManager.Application.Interfaces;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FileManager.Infrastructure.Storage;

/// <summary>
/// Resolves the active <see cref="IBlobStorage"/> implementation at runtime.
/// Resolution order:
///   1. Per-tenant BlobStorage settings (from TenantConfiguration.BlobStorage)
///   2. Global BlobStorage settings (from appsettings.json → BlobStorage section)
///   3. <see cref="NullBlobStorage"/> — no-op when not configured
/// </summary>
public class BlobStorageFactory
{
    private readonly IConfiguration _configuration;
    private readonly ITenantContext? _tenantContext;
    private readonly ILogger<CloudflareR2Storage> _r2Logger;
    private readonly ILogger<NullBlobStorage> _nullLogger;

    public BlobStorageFactory(
        IConfiguration configuration,
        ILogger<CloudflareR2Storage> r2Logger,
        ILogger<NullBlobStorage> nullLogger,
        ITenantContext? tenantContext = null)
    {
        _configuration = configuration;
        _tenantContext = tenantContext;
        _r2Logger = r2Logger;
        _nullLogger = nullLogger;
    }

    public IBlobStorage Create()
    {
        // 1. Try per-tenant settings
        var tenantBlobSettings = _tenantContext?.CurrentTenant?.Configuration?.BlobStorage;
        if (tenantBlobSettings != null && !string.IsNullOrWhiteSpace(tenantBlobSettings.Provider))
        {
            return BuildFromSettings(tenantBlobSettings);
        }

        // 2. Try global appsettings.json settings
        var globalSettings = _configuration.GetSection("BlobStorage").Get<BlobStorageSettings>();
        if (globalSettings != null && !string.IsNullOrWhiteSpace(globalSettings.Provider))
        {
            return BuildFromSettings(globalSettings);
        }

        // 3. No configuration — return no-op
        return new NullBlobStorage();
    }

    private IBlobStorage BuildFromSettings(BlobStorageSettings settings)
    {
        return settings.Provider?.ToLowerInvariant() switch
        {
            "cloudflarer2" or "r2" => new CloudflareR2Storage(settings.CloudflareR2, _r2Logger),
            _ => new NullBlobStorage()
        };
    }
}
