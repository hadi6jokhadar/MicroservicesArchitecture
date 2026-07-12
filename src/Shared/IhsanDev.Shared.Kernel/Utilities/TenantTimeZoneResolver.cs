namespace IhsanDev.Shared.Kernel.Utilities;

/// <summary>
/// Resolves a tenant's configured IANA time zone id with a safe UTC fallback.
/// Dependency-free by design: background jobs that loop over many tenants call this directly
/// per tenant (no single "current" tenant context exists in that scenario), while request-scoped
/// code goes through ITenantTimeService instead.
/// </summary>
public static class TenantTimeZoneResolver
{
    public const string DefaultTimeZoneId = "UTC";

    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static DateTime ConvertUtcToTenantLocal(DateTime utc, string? timeZoneId) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Resolve(timeZoneId));

    public static DateTime ConvertTenantLocalToUtc(DateTime local, string? timeZoneId) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Resolve(timeZoneId));

    public static bool IsValidTimeZoneId(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
