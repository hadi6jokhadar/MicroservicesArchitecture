using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Backup.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Storage;

/// <summary>
/// Cloudflare R2 blob storage implementation for Backup files, using the S3-compatible API.
/// Mirrors FileManager's <c>CloudflareR2Storage</c> shape (including the R2
/// <c>UseChunkEncoding = false</c> gotcha) but reads its own <c>BlobStorage:CloudflareR2</c>
/// settings section and targets its own bucket — Backup does not reference FileManager's classes.
/// Only registered (see <c>InfrastructureServiceExtensions.AddInfrastructureServices</c>) when all
/// required settings are present, so <see cref="IsConfigured"/> is always <c>true</c> here.
/// </summary>
public class CloudflareR2BackupStorage : IBackupBlobStorage
{
    private readonly BackupCloudflareR2Settings _settings;
    private readonly ILogger<CloudflareR2BackupStorage> _logger;
    private readonly AmazonS3Client _client;

    public bool IsConfigured => true;

    public CloudflareR2BackupStorage(BackupCloudflareR2Settings settings, ILogger<CloudflareR2BackupStorage> logger)
    {
        _settings = settings;
        _logger = logger;

        var endpoint = $"https://{settings.AccountId}.r2.cloudflarestorage.com";
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            // R2 uses "auto" region; us-east-1 is an accepted alias for SDK compatibility
            AuthenticationRegion = "auto"
        };

        var credentials = new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey);
        _client = new AmazonS3Client(credentials, config);
    }

    public async Task<string> UploadAsync(string objectKey, string localFilePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(localFilePath);

        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey,
            InputStream = stream,
            AutoCloseStream = false,
            // R2 does not support chunked streaming signatures — disable chunk encoding so the
            // SDK uses a standard signed request instead. Same gotcha as FileManager's R2 client.
            UseChunkEncoding = false
        };

        await _client.PutObjectAsync(request, ct);

        _logger.LogInformation("Backup file uploaded to Cloudflare R2. Key: {Key}", objectKey);

        return objectKey;
    }

    public async Task DownloadAsync(string objectKey, string destinationFilePath, CancellationToken ct)
    {
        var request = new GetObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey
        };

        using var response = await _client.GetObjectAsync(request, ct);

        var directory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = File.Create(destinationFilePath);
        await response.ResponseStream.CopyToAsync(fileStream, ct);

        _logger.LogInformation("Backup file downloaded from Cloudflare R2. Key: {Key}", objectKey);
    }
}
