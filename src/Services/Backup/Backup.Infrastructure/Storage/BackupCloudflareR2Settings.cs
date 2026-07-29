namespace Backup.Infrastructure.Storage;

/// <summary>
/// Backup's own Cloudflare R2 settings, bound from <c>BlobStorage:CloudflareR2</c> in
/// <c>Backup.API/appsettings.json</c>. Intentionally not shared with FileManager's
/// <c>CloudflareR2Settings</c> — Backup has no per-tenant override concept, just one global
/// bucket (e.g. <c>microservice-backups</c>).
/// </summary>
public class BackupCloudflareR2Settings
{
    public string? AccountId { get; set; }

    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    public string? BucketName { get; set; }

    public string? PublicDomain { get; set; }
}
