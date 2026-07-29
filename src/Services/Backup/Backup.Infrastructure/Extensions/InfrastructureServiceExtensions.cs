using Backup.Application.Interfaces;
using Backup.Infrastructure.Clients;
using Backup.Infrastructure.Jobs;
using Backup.Infrastructure.Persistence;
using Backup.Infrastructure.Services;
using Backup.Infrastructure.Storage;
using IhsanDev.Shared.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers the Backup service's database context (Strategy A — single global DB, no
    /// multi-tenancy), the Tenant Service directory client, blob storage (Cloudflare R2 or a
    /// no-op), the pg_dump/pg_restore process runner, and every background job.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        services.AddDatabaseContext<BackupDbContext>(
            configuration,
            migrationAssembly: typeof(BackupDbContext).Assembly.GetName().Name);

        // Tenant Service directory — used to discover active tenants and their per-tenant
        // database connection strings for tenant-scoped backup targets.
        services.AddTenantServiceClient<ITenantDirectoryClient, TenantDirectoryClient>(
            configuration, "BackupService", isDevelopment);

        // Blob storage (Cloudflare R2 or no-op) — single global configuration, no per-tenant
        // override concept (unlike FileManager's BlobStorageFactory).
        var r2Settings = configuration.GetSection("BlobStorage:CloudflareR2").Get<BackupCloudflareR2Settings>();
        var provider = configuration["BlobStorage:Provider"];

        if (string.Equals(provider, "CloudflareR2", StringComparison.OrdinalIgnoreCase)
            && r2Settings != null
            && !string.IsNullOrWhiteSpace(r2Settings.AccountId)
            && !string.IsNullOrWhiteSpace(r2Settings.AccessKeyId)
            && !string.IsNullOrWhiteSpace(r2Settings.SecretAccessKey)
            && !string.IsNullOrWhiteSpace(r2Settings.BucketName))
        {
            services.AddSingleton(r2Settings);
            services.AddSingleton<IBackupBlobStorage, CloudflareR2BackupStorage>();
        }
        else
        {
            services.AddSingleton<IBackupBlobStorage, NullBackupBlobStorage>();
        }

        // pg_dump / pg_restore process runner.
        services.AddSingleton<IPgToolRunner, PgToolRunner>();

        // Background jobs — invoked by Hangfire (see HangfireExtensions.RegisterBackupRecurringJobs)
        // or enqueued on demand by TriggerBackupCommandHandler/TriggerRestoreCommandHandler.
        services.AddTransient<BackupConnectionResolver>();
        services.AddTransient<TenantTargetSyncJob>();
        services.AddTransient<GlobalTargetSyncJob>();
        services.AddTransient<BackupSchedulerJob>();
        services.AddTransient<BackupRetentionCleanupJob>();
        services.AddTransient<IRunBackupJob, RunBackupJob>();
        services.AddTransient<IRunRestoreJob, RunRestoreJob>();

        return services;
    }
}
