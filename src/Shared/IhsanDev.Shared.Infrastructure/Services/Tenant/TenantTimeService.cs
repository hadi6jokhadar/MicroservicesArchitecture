using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Utilities;

namespace IhsanDev.Shared.Infrastructure.Services.Tenant;

public sealed class TenantTimeService : ITenantTimeService
{
    private readonly ITenantContext _tenantContext;

    public TenantTimeService(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public string TimeZoneId =>
        _tenantContext.CurrentTenant?.Configuration?.TimeZoneId is { Length: > 0 } id
            ? id
            : TenantTimeZoneResolver.DefaultTimeZoneId;

    public TimeZoneInfo TimeZone => TenantTimeZoneResolver.Resolve(TimeZoneId);

    public DateTime ConvertUtcToTenantLocal(DateTime utc) =>
        TenantTimeZoneResolver.ConvertUtcToTenantLocal(utc, TimeZoneId);

    public DateTime ConvertTenantLocalToUtc(DateTime local) =>
        TenantTimeZoneResolver.ConvertTenantLocalToUtc(local, TimeZoneId);
}
