namespace IhsanDev.Shared.Application.Services;

/// <summary>
/// Request-scoped access to the current tenant's business timezone.
/// Reads from ITenantContext; falls back to UTC when the tenant has no timezone configured
/// or multi-tenancy is disabled. Background jobs that iterate over many tenants should use
/// IhsanDev.Shared.Kernel.Utilities.TenantTimeZoneResolver directly instead, since there is
/// no single "current" tenant in that scenario.
/// </summary>
public interface ITenantTimeService
{
    /// <summary>The resolved IANA time zone id — the tenant's configured id, or "UTC" if unset.</summary>
    string TimeZoneId { get; }

    TimeZoneInfo TimeZone { get; }

    DateTime ConvertUtcToTenantLocal(DateTime utc);

    DateTime ConvertTenantLocalToUtc(DateTime local);
}
