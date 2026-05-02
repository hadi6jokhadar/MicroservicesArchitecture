using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Constants;
using Nasheed.Application.Interfaces;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Services;

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
                job.MarkFailed("Song not found.", null);
                await jobRepo.UpdateAsync(job, cancellationToken);
                return;
            }

            switch (job.JobType)
            {
                case IngestionJobType.FullPipeline:
                    await RunFullPipelineAsync(job, song, moodTagRepo, searchDocRepo, aiClient, songRepo, jobRepo, cancellationToken);
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
                    job.MarkFailed($"Unknown job type: {job.JobType}", null);
                    await jobRepo.UpdateAsync(job, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed.", job.Id);
            var nextRetry = job.RetryCount < job.MaxRetries ? DateTime.UtcNow.Add(RetryDelay) : (DateTime?)null;
            job.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message, nextRetry);
            await jobRepo.UpdateAsync(job, cancellationToken);
        }
    }

    private async Task RunFullPipelineAsync(
        SongIngestionJobEntity job,
        SongEntity song,
        ISongMoodTagRepository moodTagRepo,
        ISongSearchDocumentRepository searchDocRepo,
        IAiApiClient aiClient,
        ISongRepository songRepo,
        ISongIngestionJobRepository jobRepo,
        CancellationToken cancellationToken)
    {
        // Step 1: Metadata Extraction
        var metadataPrompt = BuildMetadataPrompt(song);
        var metadataJson = await aiClient.ChatAsync(
            NasheedAiKeys.ExtractionSettings,
            NasheedAiKeys.ExtractionPrompt,
            metadataPrompt,
            cancellationToken: cancellationToken);

        await ApplyMetadataAsync(song, metadataJson, moodTagRepo, songRepo, cancellationToken);

        // Step 2: Lyrics Verification (if raw lyrics exist)
        if (!string.IsNullOrEmpty(song.LyricsRaw))
        {
            var verifiedLyrics = await aiClient.ChatAsync(
                NasheedAiKeys.VerificationSettings,
                NasheedAiKeys.VerificationPrompt,
                song.LyricsRaw,
                cancellationToken: cancellationToken);

            song.SetVerifiedLyrics(verifiedLyrics, ExtractPlainText(verifiedLyrics));
            await songRepo.UpdateAsync(song, cancellationToken);
        }

        // Step 3: Embedding Generation
        var searchText = BuildSearchText(song);
        var embedding = await aiClient.EmbedAsync(
            NasheedAiKeys.EmbeddingSettings,
            searchText,
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
        song.SetState(SongState.Done);
        await songRepo.UpdateAsync(song, cancellationToken);

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
        var metadataJson = await aiClient.ChatAsync(
            NasheedAiKeys.ExtractionSettings,
            NasheedAiKeys.ExtractionPrompt,
            BuildMetadataPrompt(song),
            cancellationToken: cancellationToken);

        await ApplyMetadataAsync(song, metadataJson, moodTagRepo, songRepo, cancellationToken);

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
            var verifiedLyrics = await aiClient.ChatAsync(
                NasheedAiKeys.VerificationSettings,
                NasheedAiKeys.VerificationPrompt,
                song.LyricsRaw,
                cancellationToken: cancellationToken);

            song.SetVerifiedLyrics(verifiedLyrics, ExtractPlainText(verifiedLyrics));
            await songRepo.UpdateAsync(song, cancellationToken);
        }

        job.MarkCompleted();
        await jobRepo.UpdateAsync(job, cancellationToken);
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
        var searchText = BuildSearchText(song);
        var embedding = await aiClient.EmbedAsync(
            NasheedAiKeys.EmbeddingSettings,
            searchText,
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

    private static string BuildMetadataPrompt(SongEntity song)
    {
        return "Analyze the following nasheed song and extract metadata in JSON format.\n" +
               $"Title: {song.Title}\n" +
               $"Lyrics: {song.LyricsRaw ?? "Not available"}\n\n" +
               "Return a JSON object with these fields:\n" +
               "{\n" +
               "  \"language_code\": \"...\",\n" +
               "  \"summary\": \"...\",\n" +
               "  \"vocal_style\": \"...\",\n" +
               "  \"mood_tags\": [\"...\", \"...\"]\n" +
               "}";
    }

    private static async Task ApplyMetadataAsync(
        SongEntity song,
        string metadataJson,
        ISongMoodTagRepository moodTagRepo,
        ISongRepository songRepo,
        CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(ExtractJsonFromResponse(metadataJson));
            var root = doc.RootElement;

            var languageCode = root.TryGetProperty("language_code", out var lc) ? lc.GetString() : null;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : null;
            var vocalStyle = root.TryGetProperty("vocal_style", out var vs) ? vs.GetString() : null;

            song.UpdateMetadata(languageCode, lyricsRaw: null, summary, vocalStyle, durationSeconds: null);
            await songRepo.UpdateAsync(song, cancellationToken);

            if (root.TryGetProperty("mood_tags", out var moodTagsEl) && moodTagsEl.ValueKind == JsonValueKind.Array)
            {
                await moodTagRepo.DeleteBySongIdAsync(song.Id, cancellationToken);
                foreach (var tagEl in moodTagsEl.EnumerateArray())
                {
                    var tag = tagEl.GetString();
                    if (!string.IsNullOrEmpty(tag))
                    {
                        var moodTag = SongMoodTagEntity.Create(song.Id, tag);
                        await moodTagRepo.AddAsync(moodTag, cancellationToken);
                    }
                }
            }
        }
        catch
        {
            // Metadata parse failure should not fail the whole job — log and continue
        }
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
}
