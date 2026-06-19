using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace IhsanDev.Shared.Infrastructure.Services.FeatureFlags;

public sealed class TenantFeatureFlagService : IFeatureFlagService
{
    private readonly ITenantContext _tenantContext;

    public TenantFeatureFlagService(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public bool IsEnabled(string flagName, bool defaultValue = false)
    {
        var flags = _tenantContext.CurrentTenant?.Configuration?.FeatureFlags;
        return flags is not null && flags.TryGetValue(flagName, out var value)
            ? value
            : defaultValue;
    }
}
