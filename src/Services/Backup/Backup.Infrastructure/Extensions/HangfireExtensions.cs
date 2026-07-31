using Backup.Infrastructure.Jobs;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Backup.Infrastructure.Extensions;

/// <summary>
/// Backup's own Hangfire storage/dashboard/recurring-job wiring — copied in structure from
/// Category's <c>HangfireExtensions</c>. Registered unconditionally in <c>Program.cs</c> (unlike
/// Category, whose Hangfire is Redis-gated): Backup's Hangfire uses its own Postgres storage and
/// has no Redis dependency.
/// </summary>
public static class HangfireExtensions
{
    public static IServiceCollection AddBackupHangfire(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Use (IServiceProvider, IGlobalConfiguration) overload so the connection string is
        // read lazily at DI resolve time — after WebApplicationFactory test overrides apply.
        services.AddHangfire((sp, config) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>()["DatabaseSettings:ConnectionString"]
                ?? throw new InvalidOperationException("DatabaseSettings:ConnectionString not configured");
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options =>
                    options.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions { SchemaName = "hangfire_backup" });
        });

        services.AddHangfireServer(options =>
        {
            options.ServerName = "backup-hangfire";
            options.WorkerCount = 3;
            options.Queues = ["critical", "default"];
        });

        return services;
    }

    public static IApplicationBuilder UseBackupHangfireDashboard(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        app.UseHangfireDashboard("/admin/jobs/backup", new DashboardOptions
        {
            Authorization = [new HangfireBasicAuthFilter(configuration)]
        });
        return app;
    }

    public static void RegisterBackupRecurringJobs()
    {
        RecurringJob.AddOrUpdate<BackupSchedulerJob>(
            "backup-scheduler",
            job => job.RunAsync(CancellationToken.None),
            "0 1 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<BackupRetentionCleanupJob>(
            "backup-retention-cleanup",
            job => job.RunAsync(CancellationToken.None),
            "0 3 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}

/// <summary>
/// HTTP Basic Auth filter for the Hangfire dashboard.
/// Credentials are read from Hangfire:Dashboard:Username and Hangfire:Dashboard:Password in appsettings.json.
/// </summary>
internal sealed class HangfireBasicAuthFilter : IDashboardAuthorizationFilter
{
    private const int MaxAttemptsPerWindow = 5;
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, FailedAttemptWindow> FailedAttemptsByIp = new();

    private readonly string _username;
    private readonly string _password;

    public HangfireBasicAuthFilter(IConfiguration configuration)
    {
        _username = configuration["Hangfire:Dashboard:Username"]
            ?? throw new InvalidOperationException("Hangfire:Dashboard:Username not configured");
        _password = configuration["Hangfire:Dashboard:Password"]
            ?? throw new InvalidOperationException("Hangfire:Dashboard:Password not configured");
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (IsThrottled(ip))
        {
            httpContext.Response.StatusCode = 429;
            return false;
        }

        var header = httpContext.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            RecordFailure(ip);
            Challenge(httpContext);
            return false;
        }

        try
        {
            var encoded = header["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var colon = decoded.IndexOf(':');
            if (colon < 0) { RecordFailure(ip); Challenge(httpContext); return false; }

            var user = decoded[..colon];
            var pass = decoded[(colon + 1)..];

            if (FixedTimeEquals(user, _username) && FixedTimeEquals(pass, _password))
            {
                FailedAttemptsByIp.TryRemove(ip, out _);
                return true;
            }
        }
        catch
        {
            // malformed header
        }

        RecordFailure(ip);
        Challenge(httpContext);
        return false;
    }

    private static bool IsThrottled(string ip)
    {
        if (!FailedAttemptsByIp.TryGetValue(ip, out var window))
        {
            return false;
        }

        if (DateTime.UtcNow - window.WindowStart > ThrottleWindow)
        {
            FailedAttemptsByIp.TryRemove(ip, out _);
            return false;
        }

        return window.Count >= MaxAttemptsPerWindow;
    }

    private static void RecordFailure(string ip)
    {
        FailedAttemptsByIp.AddOrUpdate(
            ip,
            _ => new FailedAttemptWindow(DateTime.UtcNow, 1),
            (_, existing) => DateTime.UtcNow - existing.WindowStart > ThrottleWindow
                ? new FailedAttemptWindow(DateTime.UtcNow, 1)
                : existing with { Count = existing.Count + 1 });
    }

    // Comparing SHA-256 digests (rather than the raw byte arrays directly) sidesteps the
    // length-based timing signal CryptographicOperations.FixedTimeEquals can't hide on its own,
    // since it still requires equal-length inputs to be meaningfully constant-time.
    private static bool FixedTimeEquals(string a, string b)
    {
        var aHash = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var bHash = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(aHash, bHash);
    }

    private static void Challenge(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
    }

    private sealed record FailedAttemptWindow(DateTime WindowStart, int Count);
}
