using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Configuration;

namespace IhsanDev.Shared.Infrastructure.Services.Tenant;

/// <summary>
/// Scoped service that holds the current tenant context for the request
/// </summary>
public class TenantContext : ITenantContext
{
    private TenantInfo? _currentTenant;
    private readonly bool _isMultiTenantMode;

    public TenantContext(IConfiguration configuration)
    {
        // Check if multi-tenancy is enabled in configuration
        _isMultiTenantMode = configuration.GetValue<bool>("MultiTenancy:Enabled", false);
    }

    public TenantInfo? CurrentTenant => _currentTenant;

    public string? TenantId => _currentTenant?.TenantId;

    public bool IsMultiTenantMode => _isMultiTenantMode;

    public bool HasTenant => _currentTenant != null;

    public void SetTenant(TenantInfo? tenantInfo)
    {
        _currentTenant = tenantInfo;
    }
}
