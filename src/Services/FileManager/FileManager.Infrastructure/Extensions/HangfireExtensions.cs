using FileManager.Infrastructure.Jobs;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FileManager.Infrastructure.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddFileManagerHangfire(
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
                    new PostgreSqlStorageOptions { SchemaName = "hangfire_filemanager" });
        });

        services.AddHangfireServer(options =>
        {
            options.ServerName = "filemanager-hangfire";
            options.WorkerCount = 2;
            options.Queues = ["default", "low"];
        });

        services.AddTransient<TempFileCleanupJob>();

        return services;
    }

    public static IApplicationBuilder UseFileManagerHangfireDashboard(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        app.UseHangfireDashboard("/admin/jobs/filemanager", new DashboardOptions
        {
            Authorization = [new HangfireBasicAuthFilter(configuration)]
        });
        return app;
    }

    public static void RegisterFileManagerRecurringJobs()
    {
        RecurringJob.AddOrUpdate<TempFileCleanupJob>(
            "filemanager-temp-cleanup",
            job => job.RunAsync(CancellationToken.None),
            "0 2 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}

/// <summary>
/// HTTP Basic Auth filter for the Hangfire dashboard.
/// Credentials are read from Hangfire:Dashboard:Username and Hangfire:Dashboard:Password in appsettings.json.
/// </summary>
internal sealed class HangfireBasicAuthFilter : IDashboardAuthorizationFilter
{
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
        var header = httpContext.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Challenge(httpContext);
            return false;
        }

        try
        {
            var encoded = header["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var colon = decoded.IndexOf(':');
            if (colon < 0) { Challenge(httpContext); return false; }

            var user = decoded[..colon];
            var pass = decoded[(colon + 1)..];

            if (user == _username && pass == _password)
                return true;
        }
        catch
        {
            // malformed header
        }

        Challenge(httpContext);
        return false;
    }

    private static void Challenge(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
    }
}
