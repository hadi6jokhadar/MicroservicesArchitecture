namespace Nasheed.Application.Interfaces;

/// <summary>
/// Contract for calling AI.API from the Nasheed service.
/// Implementations live in Infrastructure.
/// </summary>
public interface IAiApiClient
{
    /// <summary>
    /// Sends a chat request to AI.API with a given settings key and system prompt key.
    /// Returns the assistant's text response.
    /// </summary>
    /// <param name="pipelineRunId">Optional correlation id (e.g. "nasheed:job:123") tagging the
    /// AI.API chat session and token usage log row so every AI call belonging to one ingestion job
    /// can be found together in AI.API's admin UI. Never affects prompt content.</param>
    Task<string> ChatAsync(
        string settingsKey,
        string systemPromptKey,
        string? userMessage = null,
        string? tenantId = null,
        IReadOnlyList<int>? fileIds = null,
        string? pipelineRunId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embedding vector for the given input text using the specified AI settings key.
    /// Returns the embedding as a float array.
    /// </summary>
    Task<float[]> EmbedAsync(
        string settingsKey,
        string inputText,
        string? tenantId = null,
        string? pipelineRunId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes a single audio file with word-level timestamps via a dedicated ASR
    /// model (real audio alignment, not LLM-estimated timing). Used by the "new" lyrics/timing
    /// extraction pipeline (feature-flagged) — see Doc/AI_INTEGRATION.md.
    /// </summary>
    Task<AiTranscriptionResult> TranscribeAsync(
        string settingsKey,
        int fileId,
        string? tenantId = null,
        string? language = null,
        string? pipelineRunId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of an AI.API ASR transcription call — real audio-aligned timing, not LLM-estimated.</summary>
public sealed class AiTranscriptionResult
{
    public string Text { get; init; } = string.Empty;
    public string? Language { get; init; }
    public double? Duration { get; init; }
    public List<AiTranscriptionWord> Words { get; init; } = [];

    /// <summary>Segment-level confidence signals — used only to filter out likely-hallucinated
    /// words before they reach the correction pass (see NasheedIngestionWorker.FilterHallucinatedWords).
    /// Not used for LRC timing; Words carries the timestamps used for that.</summary>
    public List<AiTranscriptionSegment> Segments { get; init; } = [];

    /// <summary>Admin-configured hallucination-filter threshold from AiProviderSettings.NoSpeechProbThreshold
    /// on the nasheed:transcription:settings row. Null means the admin hasn't set one — the worker
    /// falls back to its own hardcoded default in that case.</summary>
    public double? NoSpeechProbThreshold { get; init; }
}

/// <summary>One ASR-aligned word: start/end are seconds from the start of the audio.</summary>
public sealed class AiTranscriptionWord
{
    public double Start { get; init; }
    public double End { get; init; }
    public string Word { get; init; } = string.Empty;
}

/// <summary>One ASR-aligned segment, carrying Whisper's own confidence signals for that stretch of
/// audio. AvgLogprob/NoSpeechProb are null if the configured model doesn't report them.</summary>
public sealed class AiTranscriptionSegment
{
    public double Start { get; init; }
    public double End { get; init; }
    public string Text { get; init; } = string.Empty;
    public double? AvgLogprob { get; init; }
    public double? NoSpeechProb { get; init; }
}
