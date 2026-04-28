using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using Microsoft.Extensions.Logging;

namespace FileManager.Infrastructure.Storage;

/// <summary>
/// Cloudflare R2 blob storage implementation using the S3-compatible API.
/// Endpoint: https://{AccountId}.r2.cloudflarestorage.com
/// Region: auto (us-east-1 alias is accepted for SDK compatibility)
/// </summary>
public class CloudflareR2Storage : IBlobStorage
{
    private readonly CloudflareR2Settings? _settings;
    private readonly ILogger<CloudflareR2Storage> _logger;
    private readonly AmazonS3Client? _client;

    public bool IsConfigured => _client != null;

    public CloudflareR2Storage(CloudflareR2Settings? settings, ILogger<CloudflareR2Storage> logger)
    {
        _settings = settings;
        _logger = logger;

        if (settings != null
            && !string.IsNullOrWhiteSpace(settings.AccountId)
            && !string.IsNullOrWhiteSpace(settings.AccessKeyId)
            && !string.IsNullOrWhiteSpace(settings.SecretAccessKey)
            && !string.IsNullOrWhiteSpace(settings.BucketName))
        {
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
    }

    public async Task<string> UploadAsync(
        string objectKey,
        Stream stream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || _settings == null)
        {
            throw new InvalidOperationException("Blob storage is not configured.");
        }

        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
            // R2 does not support chunked streaming signatures (STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER).
            // Disable chunk encoding so the SDK uses a standard signed request instead.
            UseChunkEncoding = false
        };

        await _client.PutObjectAsync(request, cancellationToken);

        var publicDomain = _settings.PublicDomain?.TrimEnd('/') ?? string.Empty;
        var publicUrl = string.IsNullOrEmpty(publicDomain)
            ? $"https://{_settings.BucketName}.{_settings.AccountId}.r2.cloudflarestorage.com/{objectKey}"
            : $"{publicDomain}/{objectKey}";

        _logger.LogInformation("File uploaded to Cloudflare R2. Key: {Key}, URL: {Url}", objectKey, publicUrl);

        return publicUrl;
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (_client == null || _settings == null)
        {
            throw new InvalidOperationException("Blob storage is not configured.");
        }

        var request = new DeleteObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey
        };

        await _client.DeleteObjectAsync(request, cancellationToken);

        _logger.LogInformation("File deleted from Cloudflare R2. Key: {Key}", objectKey);
    }
}
