using System.Text.Json;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Constants;
using Nasheed.Application.Interfaces;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Infrastructure.Workers;

public class NasheedIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INasheedTenantCache _tenantCache;
    private readonly ILogger<NasheedIngestionWorker> _logger;
    private const int BatchSize = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    public NasheedIngestionWorker(
        IServiceScopeFactory scopeFactory,
        INasheedTenantCache tenantCache,
        ILogger<NasheedIngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _tenantCache = tenantCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NasheedIngestionWorker waiting for tenant configuration...");

        // Wait until NasheedTenantLoaderService has populated the tenant cache
        await _tenantCache.WaitUntilReadyAsync(stoppingToken);

        _logger.LogInformation("NasheedIngestionWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in ingestion worker poll cycle.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<ISongIngestionJobRepository>();
        var songRepo = scope.ServiceProvider.GetRequiredService<ISongRepository>();
        var moodTagRepo = scope.ServiceProvider.GetRequiredService<ISongMoodTagRepository>();
        var searchDocRepo = scope.ServiceProvider.GetRequiredService<ISongSearchDocumentRepository>();
        var aiClient = scope.ServiceProvider.GetRequiredService<IAiApiClient>();

        var jobs = await jobRepo.GetPendingJobsAsync(BatchSize, cancellationToken);

        foreach (var job in jobs)
        {
            await ProcessJobAsync(job, jobRepo, songRepo, moodTagRepo, searchDocRepo, aiClient, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(
        SongIngestionJobEntity job,
        ISongIngestionJobRepository jobRepo,
        ISongRepository songRepo,
        ISongMoodTagRepository moodTagRepo,
        ISongSearchDocumentRepository searchDocRepo,
        IAiApiClient aiClient,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing job {JobId} type {JobType} for song {SongId}.", job.Id, job.JobType, job.SongId);

        job.MarkRunning();
        await jobRepo.UpdateAsync(job, cancellationToken);

        try
        {
            var song = await songRepo.GetByIdAsync(job.SongId, cancellationToken);
            if (song == null)
            {
                job.MarkFailed("Song not found.", null, retryable: false);
                await jobRepo.UpdateAsync(job, cancellationToken);
                return;
            }

            switch (job.JobType)
            {
                case IngestionJobType.FullPipeline:
                    await RunFullPipelineAsync(job, song, moodTagRepo, aiClient, songRepo, jobRepo, cancellationToken);
                    break;
                case IngestionJobType.MetadataExtraction:
                    await RunMetadataExtractionAsync(job, song, moodTagRepo, aiClient, songRepo, jobRepo, cancellationToken);
                    break;
                case IngestionJobType.LyricsVerification:
                    await RunLyricsVerificationAsync(job, song, aiClient, songRepo, jobRepo, cancellationToken);
                    break;
                case IngestionJobType.EmbeddingGeneration:
                    await RunEmbeddingGenerationAsync(job, song, searchDocRepo, aiClient, songRepo, jobRepo, cancellationToken);
                    break;
                default:
                    job.MarkFailed($"Unknown job type: {job.JobType}", null, retryable: false);
                    await jobRepo.UpdateAsync(job, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed.", job.Id);
            var retryable = IsRetryableFailure(ex);
            var nextRetry = retryable && job.RetryCount < job.MaxRetries
                ? DateTime.UtcNow.Add(RetryDelay)
                : (DateTime?)null;

            var errorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            job.MarkFailed(errorMessage, nextRetry, retryable);
            await jobRepo.UpdateAsync(job, cancellationToken);
        }
    }

    private static bool IsRetryableFailure(Exception exception)
    {
        if (exception is HttpRequestException { StatusCode: HttpStatusCode statusCode })
        {
            return statusCode == HttpStatusCode.RequestTimeout
                || statusCode == HttpStatusCode.TooManyRequests
                || (int)statusCode >= 500;
        }

        return true;
    }

    private async Task RunFullPipelineAsync(
        SongIngestionJobEntity job,
        SongEntity song,
        ISongMoodTagRepository moodTagRepo,
        IAiApiClient aiClient,
        ISongRepository songRepo,
        ISongIngestionJobRepository jobRepo,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var fileIds = BuildAiFileIds(song.FileId);

        var metadataJson = await aiClient.ChatAsync(
            NasheedAiKeys.ExtractionSettings,
            NasheedAiKeys.ExtractionPrompt,
            "Analyze this audio and generate the JSON output.",
            tenantId,
            fileIds,
            cancellationToken: cancellationToken);

        await ApplyMetadataAsync(song, metadataJson, moodTagRepo, songRepo, cancellationToken);

        song.SetState(SongState.Done);
        await songRepo.UpdateAsync(song, cancellationToken);

        await QueueEmbeddingGenerationAsync(song, jobRepo, songRepo, cancellationToken);

        job.MarkCompleted();
        await jobRepo.UpdateAsync(job, cancellationToken);

        _logger.LogInformation("Full pipeline completed for song {SongId}.", song.Id);
    }

    private async Task RunMetadataExtractionAsync(
        SongIngestionJobEntity job,
        SongEntity song,
        ISongMoodTagRepository moodTagRepo,
        IAiApiClient aiClient,
        ISongRepository songRepo,
        ISongIngestionJobRepository jobRepo,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var fileIds = BuildAiFileIds(song.FileId);

        var metadataJson = await aiClient.ChatAsync(
            NasheedAiKeys.ExtractionSettings,
            NasheedAiKeys.ExtractionPrompt,
            "Analyze this audio and generate the JSON output.",
            tenantId,
            fileIds,
            cancellationToken: cancellationToken);

        await ApplyMetadataAsync(song, metadataJson, moodTagRepo, songRepo, cancellationToken);

        await QueueEmbeddingGenerationAsync(song, jobRepo, songRepo, cancellationToken);

        job.MarkCompleted();
        await jobRepo.UpdateAsync(job, cancellationToken);
    }

    private async Task RunLyricsVerificationAsync(
        SongIngestionJobEntity job,
        SongEntity song,
        IAiApiClient aiClient,
        ISongRepository songRepo,
        ISongIngestionJobRepository jobRepo,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(song.LyricsRaw))
        {
            var tenantId = GetTenantId();

            var verifiedLyrics = await aiClient.ChatAsync(
                NasheedAiKeys.ExtractionSettings,
                NasheedAiKeys.ExtractionPrompt,
                song.LyricsRaw,
                tenantId,
                cancellationToken: cancellationToken);

            song.SetVerifiedLyrics(verifiedLyrics, ExtractPlainText(verifiedLyrics));
            await songRepo.UpdateAsync(song, cancellationToken);

            await QueueEmbeddingGenerationAsync(song, jobRepo, songRepo, cancellationToken);
        }

        job.MarkCompleted();
        await jobRepo.UpdateAsync(job, cancellationToken);
    }

    private string GetTenantId() => _tenantCache.Tenant!.TenantId;

    private async Task QueueEmbeddingGenerationAsync(
        SongEntity song,
        ISongIngestionJobRepository jobRepo,
        ISongRepository songRepo,
        CancellationToken cancellationToken)
    {
        var hasActiveEmbeddingJob = await jobRepo.HasActiveJobAsync(song.Id, IngestionJobType.EmbeddingGeneration, cancellationToken);
        if (hasActiveEmbeddingJob)
            return;

        try
        {
            var embeddingJob = SongIngestionJobEntity.Create(song.Id, song.FileId, IngestionJobType.EmbeddingGeneration);
            await jobRepo.AddAsync(embeddingJob, cancellationToken);

            song.SetSearchIndexStatus(SearchIndexStatus.Indexing);
            await songRepo.UpdateAsync(song, cancellationToken);

            _logger.LogInformation("Queued embedding job {JobId} for song {SongId} after song data update.", embeddingJob.Id, song.Id);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Another concurrent request created the same active job — safe to ignore.
        }
    }

    private async Task RunEmbeddingGenerationAsync(
        SongIngestionJobEntity job,
        SongEntity song,
        ISongSearchDocumentRepository searchDocRepo,
        IAiApiClient aiClient,
        ISongRepository songRepo,
        ISongIngestionJobRepository jobRepo,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var searchText = BuildSearchText(song);
        var embedding = await aiClient.EmbedAsync(
            NasheedAiKeys.EmbeddingSettings,
            searchText,
            tenantId,
            cancellationToken: cancellationToken);

        var embeddingJson = JsonSerializer.Serialize(embedding);
        var doc = await searchDocRepo.GetBySongIdAsync(song.Id, cancellationToken);
        if (doc == null)
        {
            doc = SongSearchDocumentEntity.Create(song.Id, searchText, embeddingJson, NasheedAiKeys.EmbeddingSettings);
        }
        else
        {
            doc.Update(searchText, embeddingJson, NasheedAiKeys.EmbeddingSettings);
        }
        await searchDocRepo.UpsertAsync(doc, cancellationToken);

        song.SetSearchIndexStatus(SearchIndexStatus.Indexed);
        await songRepo.UpdateAsync(song, cancellationToken);

        job.MarkCompleted();
        await jobRepo.UpdateAsync(job, cancellationToken);
    }

    private static async Task ApplyMetadataAsync(
        SongEntity song,
        string metadataJson,
        ISongMoodTagRepository moodTagRepo,
        ISongRepository songRepo,
        CancellationToken cancellationToken)
    {
        var rawJson = ExtractJsonFromResponse(metadataJson);

        if (rawJson.Length > 102_400)
            throw new InvalidOperationException("AI metadata response exceeds the 100 KB size limit.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson, new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse AI metadata JSON: {ex.Message}", ex);
        }

        using (doc)
        {
        var root = doc.RootElement;

        var languageCode = ReadString(root, "language_code", "languageCode");
        var summary = ReadString(root, "summary");
        var vocalStyle = ReadString(root, "vocal_style", "vocalStyle");
        var durationSeconds = ReadNullableInt(root, "duration_seconds", "durationSeconds");
        var lyricsRawLrc = ReadString(root, "lyrics_raw_lrc", "lyricsRawLrc", "lyrics_raw", "lyricsRaw", "lrc");
        var legalCompliance = ReadObject(root, "legal_compliance", "legalCompliance");
        if (string.IsNullOrWhiteSpace(lyricsRawLrc))
        {
            throw new InvalidOperationException("AI response does not include lyrics_raw_lrc.");
        }

        song.UpdateMetadata(languageCode, lyricsRawLrc, summary, vocalStyle, durationSeconds);

        if (legalCompliance.HasValue)
        {
            var copyrightRiskLevel = ReadString(legalCompliance.Value, "copyright_risk_level", "copyrightRiskLevel");
            var contentSafetyFlag = ReadString(legalCompliance.Value, "content_safety_flag", "contentSafetyFlag");
            var riskReason = ReadNullableString(legalCompliance.Value, "risk_reason", "riskReason");

            song.UpdateLegalComplianceFromAi(copyrightRiskLevel, contentSafetyFlag, riskReason);
        }

        await songRepo.UpdateAsync(song, cancellationToken);

        var moodTagsEl = ReadArray(root, "mood_tags", "moodTags");
        if (moodTagsEl.HasValue && moodTagsEl.Value.ValueKind == JsonValueKind.Array)
        {
            await moodTagRepo.DeleteBySongIdAsync(song.Id, cancellationToken);
            var normalizedTags = moodTagsEl.Value
                .EnumerateArray()
                .Select(tagEl => tagEl.ValueKind == JsonValueKind.String ? tagEl.GetString()?.Trim() : null)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var tag in normalizedTags)
            {
                var moodTag = SongMoodTagEntity.Create(song.Id, tag!);
                await moodTagRepo.AddAsync(moodTag, cancellationToken);
            }
        }
        } // end using (doc)
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static string? ReadNullableString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static JsonElement? ReadObject(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object)
            {
                return value;
            }
        }

        return null;
    }

    private static JsonElement? ReadArray(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                return value;
            }
        }

        return null;
    }

    private static int? ReadNullableInt(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string BuildSearchText(SongEntity song)
    {
        var parts = new List<string> { song.Title };
        if (!string.IsNullOrEmpty(song.Summary)) parts.Add(song.Summary);
        if (!string.IsNullOrEmpty(song.LyricsPlainText)) parts.Add(song.LyricsPlainText[..Math.Min(500, song.LyricsPlainText.Length)]);
        return string.Join(". ", parts);
    }

    private static string ExtractPlainText(string lrcContent)
    {
        // Strip LRC timestamps like [00:01.00] from lyrics
        var lines = lrcContent.Split('\n');
        var plainLines = lines
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"\[\d+:\d+\.\d+\]", "").Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l));
        return string.Join("\n", plainLines);
    }

    private static string ExtractJsonFromResponse(string response)
    {
        // Try to extract JSON block from markdown code fence if present
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start >= 0 && end > start)
            return response[start..(end + 1)];
        return response;
    }

    private static List<int> BuildAiFileIds(int fileId)
    {
        return [fileId];
    }
}
