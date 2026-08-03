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
    Task<string> ChatAsync(
        string settingsKey,
        string systemPromptKey,
        string? userMessage = null,
        string? tenantId = null,
        IReadOnlyList<int>? fileIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embedding vector for the given input text using the specified AI settings key.
    /// Returns the embedding as a float array.
    /// </summary>
    Task<float[]> EmbedAsync(
        string settingsKey,
        string inputText,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes a single audio file with segment-level timestamps via a dedicated ASR
    /// model (real audio alignment, not LLM-estimated timing). Used by the "new" lyrics/timing
    /// extraction pipeline (feature-flagged) — see Doc/AI_INTEGRATION.md.
    /// </summary>
    Task<AiTranscriptionResult> TranscribeAsync(
        string settingsKey,
        int fileId,
        string? tenantId = null,
        string? language = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of an AI.API ASR transcription call — real audio-aligned timing, not LLM-estimated.</summary>
public sealed class AiTranscriptionResult
{
    public string Text { get; init; } = string.Empty;
    public string? Language { get; init; }
    public double? Duration { get; init; }
    public List<AiTranscriptionSegment> Segments { get; init; } = [];
}

/// <summary>One ASR-aligned segment: start/end are seconds from the start of the audio.</summary>
public sealed class AiTranscriptionSegment
{
    public double Start { get; init; }
    public double End { get; init; }
    public string Text { get; init; } = string.Empty;
}
