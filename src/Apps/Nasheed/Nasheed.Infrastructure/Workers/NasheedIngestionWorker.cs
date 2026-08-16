using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using System.Globalization;
using System.Net;
using IhsanDev.Shared.Infrastructure.Services.Notification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Constants;
using Nasheed.Application.Interfaces;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;
using Polly.CircuitBreaker;
using SharedFeatureFlags = IhsanDev.Shared.Application.Constants.FeatureFlags;

namespace Nasheed.Infrastructure.Workers;

public class NasheedIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INasheedTenantCache _tenantCache;
    private readonly ILogger<NasheedIngestionWorker> _logger;
    private const int BatchSize = 5;

    // Default JsonSerializer escapes every non-ASCII character (Arabic, etc.) as \uXXXX — wasteful
    // (6 ASCII chars per escaped letter instead of 1) and unnecessary for a JSON payload that only
    // ever travels API-to-API, never into HTML. Used for payloads built here that contain Arabic text.
    private static readonly JsonSerializerOptions ArabicSafeJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    // Exponential back-off: attempt 1→30s, 2→2min, 3→10min, 4+→30min
    private static TimeSpan GetRetryDelay(int retryCount) => retryCount switch
    {
        1 => TimeSpan.FromSeconds(30),
        2 => TimeSpan.FromMinutes(2),
        3 => TimeSpan.FromMinutes(10),
        _ => TimeSpan.FromMinutes(30),
    };

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
                if (IsIngestionEnabled())
                {
                    await ProcessPendingJobsAsync(stoppingToken);
                }
                else
                {
                    _logger.LogDebug("Nasheed ingestion is disabled by feature flag for tenant {TenantId}.", _tenantCache.Tenant?.TenantId);
                }
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
        var notificationClient = scope.ServiceProvider.GetRequiredService<INotificationServiceClient>();

        var jobs = await jobRepo.GetPendingJobsAsync(BatchSize, cancellationToken);

        foreach (var job in jobs)
        {
            await ProcessJobAsync(job, jobRepo, songRepo, moodTagRepo, searchDocRepo, aiClient, notificationClient, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(
        SongIngestionJobEntity job,
        ISongIngestionJobRepository jobRepo,
        ISongRepository songRepo,
        ISongMoodTagRepository moodTagRepo,
        ISongSearchDocumentRepository searchDocRepo,
        IAiApiClient aiClient,
        INotificationServiceClient notificationClient,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing job {JobId} type {JobType} for song {SongId}.", job.Id, job.JobType, job.SongId);

        job.MarkRunning();
        await jobRepo.UpdateAsync(job, cancellationToken);
        await BroadcastProgressAsync(notificationClient, job, songTitle: null, cancellationToken);

        SongEntity? song = null;
        try
        {
            song = await songRepo.GetByIdAsync(job.SongId, cancellationToken);
            if (song == null)
            {
                job.MarkFailed("Song not found.", null, retryable: false);
                await jobRepo.UpdateAsync(job, cancellationToken);
                await BroadcastProgressAsync(notificationClient, job, songTitle: null, cancellationToken);
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

            await BroadcastProgressAsync(notificationClient, job, song.Title, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed.", job.Id);
            var retryable = IsRetryableFailure(ex);
            var nextRetryCount = job.RetryCount + 1;
            var nextRetry = retryable && job.RetryCount < job.MaxRetries
                ? DateTime.UtcNow.Add(GetRetryDelay(nextRetryCount))
                : (DateTime?)null;

            var errorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            job.MarkFailed(errorMessage, nextRetry, retryable);
            await jobRepo.UpdateAsync(job, cancellationToken);

            // The FullPipeline job drives the Song's own lifecycle (Uploaded/InQueue -> Done),
            // so once it has exhausted retries (MarkFailed left it at Failed, not back at Pending
            // for another automatic attempt), the Song must surface that too — otherwise it stays
            // stuck at InQueue forever with no indication processing ever stopped. A subsequent
            // manual retry (RetrySongAnalysisCommandHandler / RetryIngestionJobCommandHandler)
            // already moves the Song back to InQueue, and a successful run always sets it to Done
            // (see RunFullPipelineAsync), so this only needs to cover the terminal-failure edge.
            if (song is not null && job.JobType == IngestionJobType.FullPipeline && job.JobStatus == IngestionJobStatus.Failed)
            {
                song.SetState(SongState.Failed);
                await songRepo.UpdateAsync(song, cancellationToken);
            }

            await BroadcastProgressAsync(notificationClient, job, songTitle: null, cancellationToken);
        }
    }

    /// <summary>
    /// Pushes a job-status change to the Nasheed admin app in real time via the Notification
    /// service's SignalR hub, so the ingestion/songs tables update live instead of on refresh.
    /// deliveryType "SignalR" skips Firebase (this fires once per job-status transition, not
    /// something mobile users should get a push per event for); the "silent": true marker in
    /// the data payload tells the frontend's shared SignalrService to skip its toast popup too
    /// (see libs/shared/src/lib/services/signalr.service.ts). Never throws — a failed broadcast
    /// must not affect ingestion processing.
    /// </summary>
    private async Task BroadcastProgressAsync(
        INotificationServiceClient notificationClient,
        SongIngestionJobEntity job,
        string? songTitle,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var subject = songTitle ?? $"Song #{job.SongId}";
            var data = JsonSerializer.Serialize(new
            {
                @event = "nasheed-ingestion-progress",
                silent = true,
                songId = job.SongId,
                jobId = job.Id,
                jobType = job.JobType.ToString(),
                jobStatus = job.JobStatus.ToString(),
                retryCount = job.RetryCount,
                errorMessage = job.LastError,
            });

            var sent = await notificationClient.SendTenantBroadcastAsync(
                tenantId,
                title: "Ingestion progress",
                message: $"{subject}: {job.JobType} is now {job.JobStatus}.",
                data: data,
                deliveryType: "SignalR",
                cancellationToken: cancellationToken);

            if (!sent)
            {
                _logger.LogWarning(
                    "Failed to broadcast ingestion progress for job {JobId} (song {SongId}).",
                    job.Id, job.SongId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error broadcasting ingestion progress for job {JobId} (song {SongId}).",
                job.Id, job.SongId);
        }
    }

    private static bool IsRetryableFailure(Exception exception)
    {
        if (exception is BrokenCircuitException)
            return true;

        if (exception is HttpRequestException httpEx)
        {
            // Connection refused / no status code → service is down, retry later
            if (httpEx.StatusCode is null)
                return true;

            return httpEx.StatusCode == HttpStatusCode.RequestTimeout
                || httpEx.StatusCode == HttpStatusCode.TooManyRequests
                || (int)httpEx.StatusCode >= 500;
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
        await ExtractLyricsAndMetadataAsync(song, moodTagRepo, aiClient, songRepo, BuildPipelineRunId(job), cancellationToken);

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
        await ExtractLyricsAndMetadataAsync(song, moodTagRepo, aiClient, songRepo, BuildPipelineRunId(job), cancellationToken);

        await QueueEmbeddingGenerationAsync(song, jobRepo, songRepo, cancellationToken);

        job.MarkCompleted();
        await jobRepo.UpdateAsync(job, cancellationToken);
    }

    /// <summary>
    /// Extracts lyrics/timing + enrichment metadata for a song, routing to either the
    /// "old" single-chat-call pipeline or the "new" hybrid pipeline based on
    /// FeatureFlags.NasheedNewLyricsExtractionEnabled. See Doc/AI_INTEGRATION.md.
    /// </summary>
    private async Task ExtractLyricsAndMetadataAsync(
        SongEntity song,
        ISongMoodTagRepository moodTagRepo,
        IAiApiClient aiClient,
        ISongRepository songRepo,
        string pipelineRunId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var fileIds = BuildAiFileIds(song.FileId);

        if (IsNewLyricsExtractionEnabled())
        {
            // Step 1: the OLD pipeline's own multimodal call — proven better lyrics text quality
            // (audio-grounded, full language understanding, can recognize a known published poem)
            // than a pure-ASR-plus-text-only-correction pipeline. We only keep its lyrics lines;
            // its own timing estimate and other fields are discarded — enrichment stays its own
            // separate, focused call below, for the same reason it always has (see EnrichmentPrompt).
            var extractionJson = await aiClient.ChatAsync(
                NasheedAiKeys.ExtractionSettings,
                NasheedAiKeys.ExtractionPrompt,
                "Analyze this audio and generate the JSON output.",
                tenantId,
                fileIds,
                pipelineRunId,
                cancellationToken: cancellationToken);

            var oldLines = ExtractLrcLines(extractionJson);
            if (oldLines.Count == 0)
            {
                throw new InvalidOperationException("AI extraction response does not include usable lyrics lines.");
            }

            // Step 2: real ASR word-level timestamps — used only for timing, never for text content.
            var transcription = await aiClient.TranscribeAsync(
                NasheedAiKeys.TranscriptionSettings,
                song.FileId,
                tenantId,
                pipelineRunId: pipelineRunId,
                cancellationToken: cancellationToken);

            if (transcription.Words.Count == 0)
            {
                throw new InvalidOperationException(
                    "ASR transcription returned no word-level timestamps; nasheed:transcription:settings " +
                    "must resolve to a model that supports word-level timestamp_granularities.");
            }

            var confidentWords = FilterHallucinatedWords(
                transcription.Segments, transcription.Words, transcription.NoSpeechProbThreshold);
            if (confidentWords.Count == 0)
            {
                throw new InvalidOperationException(
                    "Every transcribed word was filtered out as likely-hallucinated " +
                    "(no_speech_prob too high across the whole file) — check the source audio.");
            }

            // Step 3: align step 1's trusted lyric lines against step 2's ASR words. Pure alignment,
            // not correction — the model never changes step 1's text, it only reports which ASR word
            // index each line starts at, so BuildLrcFromLineMappings can look up its real timestamp.
            // No audio needed here: both inputs are already-produced text.
            var mergeJson = await aiClient.ChatAsync(
                NasheedAiKeys.MergeSettings,
                NasheedAiKeys.MergePrompt,
                BuildMergePayload(oldLines, confidentWords),
                tenantId,
                pipelineRunId: pipelineRunId,
                cancellationToken: cancellationToken);

            var lineMappings = ParseLineMappings(mergeJson);
            var lyricsRawLrc = BuildLrcFromLineMappings(oldLines, confidentWords, lineMappings);
            if (string.IsNullOrWhiteSpace(lyricsRawLrc))
            {
                throw new InvalidOperationException("Timing alignment produced no usable LRC lines.");
            }

            // Whisper's own reported duration is more reliable than the extraction call's estimate.
            var durationSeconds = transcription.Duration.HasValue
                ? (int)Math.Round(transcription.Duration.Value)
                : (int?)null;

            // Step 4: unchanged — a focused, text-only enrichment call for summary/vocal_style/
            // mood_tags/legal_compliance/language_code.
            var enrichmentJson = await aiClient.ChatAsync(
                NasheedAiKeys.EnrichmentSettings,
                NasheedAiKeys.EnrichmentPrompt,
                transcription.Text,
                tenantId,
                pipelineRunId: pipelineRunId,
                cancellationToken: cancellationToken);

            await ApplyEnrichmentMetadataAsync(
                song, enrichmentJson, lyricsRawLrc, durationSeconds, transcription.Language,
                moodTagRepo, songRepo, cancellationToken);
        }
        else
        {
            var metadataJson = await aiClient.ChatAsync(
                NasheedAiKeys.ExtractionSettings,
                NasheedAiKeys.ExtractionPrompt,
                "Analyze this audio and generate the JSON output.",
                tenantId,
                fileIds,
                pipelineRunId,
                cancellationToken: cancellationToken);

            await ApplyMetadataAsync(song, metadataJson, moodTagRepo, songRepo, cancellationToken);
        }
    }

    // Whisper's own no_speech_prob for a segment — close to 1.0 means Whisper itself believes that
    // stretch of audio is silence/breath/reverb with no real speech, yet still emitted text for it.
    // A hallucinated word offered as a timing-anchor candidate to the merge call (see BuildMergePayload)
    // is just unnecessary noise the model has to reason through — cheaper and more reliable to drop it
    // here than rely on the merge call never picking a bad anchor. Deliberately not also gating on
    // avg_logprob — that can be naturally low for correctly transcribed rare/classical vocabulary or
    // melismatic singing, so using it as a hard filter would discard real content specific to this domain.
    // Default only — an admin can override this per settings row via AiProviderSettings.NoSpeechProbThreshold
    // on nasheed:transcription:settings (echoed back in AiTranscriptionResult.NoSpeechProbThreshold),
    // since no_speech_prob's right cutoff is genre/recording-dependent and shouldn't require a code
    // change to tune. Raised from an initial 0.6 to 0.85 (August 2026) after 0.6 wiped out an entire
    // song's lyrics — no_speech_prob is calibrated for spoken conversation, and sustained/melismatic
    // *singing* with reverb (i.e. exactly what acapella nasheed vocals are) can push it well above
    // 0.6 even when there is clearly audible singing, not silence.
    private const double DefaultHallucinationNoSpeechProbThreshold = 0.85;

    // Safety net for the same incident: even at a better-tuned threshold, no_speech_prob can still be
    // miscalibrated for a specific recording (heavy reverb, unusual vocal style). If the filter would
    // remove more than this fraction of all words, that's a stronger signal the heuristic itself is
    // unreliable for this file than that the song is genuinely mostly silent — skip filtering entirely
    // rather than risk repeating the all-lyrics-missing failure. Worst case without this cap tripping,
    // some hallucinated junk reaches the correction pass, same risk as before this feature existed;
    // worst case if it trips unnecessarily, a genuinely near-silent file's junk isn't filtered — both
    // far better than silently deleting real lyrics.
    private const double MaxHallucinatedWordFraction = 0.4;

    /// <summary>Drops words whose containing ASR segment has a no_speech_prob above
    /// <paramref name="configuredThreshold"/> (falls back to <see cref="DefaultHallucinationNoSpeechProbThreshold"/>
    /// when null — see that constant's comment for why). Backs off entirely (returns all words
    /// unfiltered) if that would remove more than <see cref="MaxHallucinatedWordFraction"/> of the
    /// song — see that constant's comment.</summary>
    private List<AiTranscriptionWord> FilterHallucinatedWords(
        IReadOnlyList<AiTranscriptionSegment> segments,
        IReadOnlyList<AiTranscriptionWord> words,
        double? configuredThreshold)
    {
        var threshold = configuredThreshold ?? DefaultHallucinationNoSpeechProbThreshold;
        var hallucinatedRanges = segments
            .Where(s => s.NoSpeechProb is double p && p > threshold)
            .ToList();

        if (hallucinatedRanges.Count == 0)
        {
            return words.ToList();
        }

        var filtered = new List<AiTranscriptionWord>(words.Count);
        var droppedWords = new List<string>();

        foreach (var word in words)
        {
            var isHallucinated = hallucinatedRanges.Any(r => word.Start >= r.Start && word.Start < r.End);
            if (isHallucinated)
            {
                droppedWords.Add(word.Word);
            }
            else
            {
                filtered.Add(word);
            }
        }

        if (droppedWords.Count == 0)
        {
            return filtered;
        }

        var droppedFraction = (double)droppedWords.Count / words.Count;
        if (droppedFraction > MaxHallucinatedWordFraction)
        {
            _logger.LogWarning(
                "Hallucination filter would drop {DroppedCount}/{TotalCount} words ({Fraction:P0}), " +
                "exceeding the {Cap:P0} safety cap — skipping filtering entirely for this file instead " +
                "of risking real lyrics loss. Flagged segment no_speech_prob values: {Probs}",
                droppedWords.Count, words.Count, droppedFraction, MaxHallucinatedWordFraction,
                string.Join(", ", hallucinatedRanges.Select(r => r.NoSpeechProb)));
            return words.ToList();
        }

        _logger.LogWarning(
            "Dropped {Count}/{Total} likely-hallucinated word(s) (no_speech_prob > {Threshold}): {Words}",
            droppedWords.Count, words.Count, threshold, string.Join(' ', droppedWords));

        return filtered;
    }

    private bool IsNewLyricsExtractionEnabled()
    {
        var flags = _tenantCache.Tenant?.Configuration?.FeatureFlags;
        return flags is not null
            && flags.TryGetValue(SharedFeatureFlags.NasheedNewLyricsExtractionEnabled, out var enabled)
            && enabled;
    }

    /// <summary>Pulls the extraction call's lyrics_raw_lrc out of its JSON response and strips LRC
    /// timestamps, returning the trusted lyric lines in order — these are never rewritten by the
    /// merge step, only anchored to real timestamps.</summary>
    private static List<string> ExtractLrcLines(string metadataJson)
    {
        var rawJson = ExtractJsonFromResponse(metadataJson);
        if (rawJson.Length > 102_400)
            throw new InvalidOperationException("AI extraction response exceeds the 100 KB size limit.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson, new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse AI extraction JSON: {ex.Message}", ex);
        }

        string? lyricsRawLrc;
        using (doc)
        {
            lyricsRawLrc = ReadString(doc.RootElement, "lyrics_raw_lrc", "lyricsRawLrc", "lyrics_raw", "lyricsRaw", "lrc");
        }

        if (string.IsNullOrWhiteSpace(lyricsRawLrc))
            return [];

        var plainText = ExtractPlainText(lyricsRawLrc);
        return string.IsNullOrWhiteSpace(plainText) ? [] : plainText.Split('\n').ToList();
    }

    /// <summary>Serializes the trusted lyric lines (numbered) and ASR words (indexed) for the merge
    /// chat call — the model reports which ASR word index each line starts at, for timing only.</summary>
    private static string BuildMergePayload(IReadOnlyList<string> lines, IReadOnlyList<AiTranscriptionWord> words)
    {
        var payload = new
        {
            lines = lines.Select((text, n) => new { n, text }).ToList(),
            words = words.Select((w, i) => new { i, w = w.Word }).ToList(),
        };
        return JsonSerializer.Serialize(payload, ArabicSafeJsonOptions);
    }

    /// <summary>Parses the merge chat response's <c>{"mappings":[{"line":N,"start_index":M}]}</c>
    /// shape — each entry says lyric line N starts at ASR word index M.</summary>
    private static List<(int LineIndex, int StartIndex)> ParseLineMappings(string mergeJson)
    {
        var rawJson = ExtractJsonFromResponse(mergeJson);
        if (rawJson.Length > 204_800)
            throw new InvalidOperationException("AI merge response exceeds the 200 KB size limit.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson, new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse AI merge JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            var mappingsEl = ReadArray(doc.RootElement, "mappings");
            if (!mappingsEl.HasValue)
                throw new InvalidOperationException("AI merge response does not include a mappings array.");

            var mappings = new List<(int LineIndex, int StartIndex)>();
            foreach (var mappingEl in mappingsEl.Value.EnumerateArray())
            {
                var lineIndex = ReadNullableInt(mappingEl, "line", "line_index", "lineIndex");
                var startIndex = ReadNullableInt(mappingEl, "start_index", "startIndex");
                if (lineIndex is null || startIndex is null)
                    continue;

                mappings.Add((lineIndex.Value, startIndex.Value));
            }

            if (mappings.Count == 0)
                throw new InvalidOperationException("AI merge response contained no usable line mappings.");

            return mappings;
        }
    }

    /// <summary>Builds LRC text from the trusted lyric lines, using the merge call's line→word-index
    /// mappings to look up each line's real ASR-aligned timestamp. A line the merge call didn't map
    /// gets an interpolated timestamp from its nearest mapped neighbors (see
    /// <see cref="InterpolateMissingLineTimestamps"/>) rather than being silently dropped.</summary>
    private static string BuildLrcFromLineMappings(
        IReadOnlyList<string> lines,
        IReadOnlyList<AiTranscriptionWord> words,
        IReadOnlyList<(int LineIndex, int StartIndex)> mappings)
    {
        var lineStartSeconds = new double?[lines.Count];
        foreach (var (lineIndex, startIndex) in mappings)
        {
            if (lineIndex >= 0 && lineIndex < lines.Count && startIndex >= 0 && startIndex < words.Count)
            {
                lineStartSeconds[lineIndex] ??= words[startIndex].Start;
            }
        }

        InterpolateMissingLineTimestamps(lineStartSeconds);

        var sb = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            var text = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(text) || lineStartSeconds[i] is not double startSeconds)
                continue;

            AppendLrcLine(sb, startSeconds, text);
        }

        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>Fills any line the merge call didn't map to a word by linearly interpolating between
    /// its nearest mapped neighbors (or carrying the nearest known value at the start/end of the
    /// song), so an occasional missed mapping never drops a line out of the final lyrics.</summary>
    private static void InterpolateMissingLineTimestamps(double?[] lineStartSeconds)
    {
        var knownIndices = new List<int>();
        for (var i = 0; i < lineStartSeconds.Length; i++)
        {
            if (lineStartSeconds[i].HasValue)
                knownIndices.Add(i);
        }

        if (knownIndices.Count == 0)
            return;

        for (var i = 0; i < knownIndices[0]; i++)
            lineStartSeconds[i] = lineStartSeconds[knownIndices[0]];

        for (var i = knownIndices[^1] + 1; i < lineStartSeconds.Length; i++)
            lineStartSeconds[i] = lineStartSeconds[knownIndices[^1]];

        for (var k = 0; k < knownIndices.Count - 1; k++)
        {
            var a = knownIndices[k];
            var b = knownIndices[k + 1];
            if (b - a <= 1)
                continue;

            var startValue = lineStartSeconds[a]!.Value;
            var endValue = lineStartSeconds[b]!.Value;
            var step = (endValue - startValue) / (b - a);
            for (var i = a + 1; i < b; i++)
            {
                lineStartSeconds[i] = startValue + step * (i - a);
            }
        }
    }

    private static void AppendLrcLine(StringBuilder sb, double startSeconds, string text)
    {
        var totalHundredths = (long)Math.Round(startSeconds * 100, MidpointRounding.AwayFromZero);
        if (totalHundredths < 0)
            totalHundredths = 0;
        var minutes = totalHundredths / 6000;
        var secondsHundredths = totalHundredths % 6000;

        sb.Append('[')
          .Append(minutes.ToString("D2", CultureInfo.InvariantCulture)).Append(':')
          .Append((secondsHundredths / 100).ToString("D2", CultureInfo.InvariantCulture)).Append('.')
          .Append((secondsHundredths % 100).ToString("D2", CultureInfo.InvariantCulture))
          .Append(']').Append(text).Append('\n');
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
                pipelineRunId: BuildPipelineRunId(job),
                cancellationToken: cancellationToken);

            song.SetVerifiedLyrics(verifiedLyrics, ExtractPlainText(verifiedLyrics));
            await songRepo.UpdateAsync(song, cancellationToken);

            await QueueEmbeddingGenerationAsync(song, jobRepo, songRepo, cancellationToken);
        }

        job.MarkCompleted();
        await jobRepo.UpdateAsync(job, cancellationToken);
    }

    private bool IsIngestionEnabled()
    {
        var flags = _tenantCache.Tenant?.Configuration?.FeatureFlags;
        return flags is null || !flags.TryGetValue(SharedFeatureFlags.NasheedIngestionEnabled, out var enabled) || enabled;
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

        float[] embedding;
        try
        {
            embedding = await aiClient.EmbedAsync(
                NasheedAiKeys.EmbeddingSettings,
                searchText,
                tenantId,
                pipelineRunId: BuildPipelineRunId(job),
                cancellationToken: cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "AI circuit open; embedding job {JobId} for song {SongId} will retry.", job.Id, song.Id);
            throw;
        }

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

        await ApplyMoodTagsAsync(song, root, moodTagRepo, cancellationToken);
        } // end using (doc)
    }

    /// <summary>
    /// Applies enrichment metadata (summary/vocal_style/mood_tags/legal_compliance/language_code)
    /// from a text-only AI response, using ASR-derived lyrics/duration/language passed in directly
    /// rather than parsed from the response — see the "new" pipeline branch in
    /// <see cref="ExtractLyricsAndMetadataAsync"/>.
    /// </summary>
    private static async Task ApplyEnrichmentMetadataAsync(
        SongEntity song,
        string enrichmentJson,
        string lyricsRawLrc,
        int? durationSeconds,
        string? asrLanguage,
        ISongMoodTagRepository moodTagRepo,
        ISongRepository songRepo,
        CancellationToken cancellationToken)
    {
        var rawJson = ExtractJsonFromResponse(enrichmentJson);

        if (rawJson.Length > 102_400)
            throw new InvalidOperationException("AI enrichment response exceeds the 100 KB size limit.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson, new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse AI enrichment JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            var languageCode = ReadString(root, "language_code", "languageCode") ?? asrLanguage;
            var summary = ReadString(root, "summary");
            var vocalStyle = ReadString(root, "vocal_style", "vocalStyle");
            var legalCompliance = ReadObject(root, "legal_compliance", "legalCompliance");

            song.UpdateMetadata(languageCode, lyricsRawLrc, summary, vocalStyle, durationSeconds);

            if (legalCompliance.HasValue)
            {
                var copyrightRiskLevel = ReadString(legalCompliance.Value, "copyright_risk_level", "copyrightRiskLevel");
                var contentSafetyFlag = ReadString(legalCompliance.Value, "content_safety_flag", "contentSafetyFlag");
                var riskReason = ReadNullableString(legalCompliance.Value, "risk_reason", "riskReason");

                song.UpdateLegalComplianceFromAi(copyrightRiskLevel, contentSafetyFlag, riskReason);
            }

            await songRepo.UpdateAsync(song, cancellationToken);

            await ApplyMoodTagsAsync(song, root, moodTagRepo, cancellationToken);
        }
    }

    private static async Task ApplyMoodTagsAsync(
        SongEntity song,
        JsonElement root,
        ISongMoodTagRepository moodTagRepo,
        CancellationToken cancellationToken)
    {
        var moodTagsEl = ReadArray(root, "mood_tags", "moodTags");
        if (!moodTagsEl.HasValue || moodTagsEl.Value.ValueKind != JsonValueKind.Array)
            return;

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

    /// <summary>Correlation id sent to AI.API on every AI call made while processing this job, so
    /// they can all be found together in AI.API's admin UI (Chat Sessions, token usage log) — see
    /// Doc/AI_INTEGRATION.md. Reuses the job's own id rather than minting a new identifier.</summary>
    private static string BuildPipelineRunId(SongIngestionJobEntity job) => $"nasheed:job:{job.Id}";
}
