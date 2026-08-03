namespace Nasheed.Application.Constants;

/// <summary>
/// Hardcoded keys used to resolve AI provider settings and system prompts from AI.API.
/// These keys must exist in AI.API's database for each tenant scope (or have a global fallback).
/// </summary>
public static class NasheedAiKeys
{
    public const string ExtractionSettings    = "nasheed:extraction:settings";
    public const string ExtractionPrompt      = "nasheed:extraction:system-prompt";
    public const string EmbeddingSettings     = "nasheed:embedding:settings";

    // "New" lyrics/timing extraction pipeline (feature-flagged — FeatureFlags.NasheedNewLyricsExtractionEnabled).
    // TranscriptionSettings must be an AI.API AiProviderSettings row with ModelType=Audio (a real ASR
    // model, e.g. Whisper) — timing comes from audio alignment, not from the chat model.
    // EnrichmentPrompt is a text-only prompt (no audio, no timestamp rules) that only produces
    // summary/vocal_style/mood_tags/legal_compliance/language_code from the ASR transcript.
    public const string TranscriptionSettings = "nasheed:transcription:settings";
    public const string EnrichmentSettings    = "nasheed:enrichment:settings";
    public const string EnrichmentPrompt      = "nasheed:enrichment:system-prompt";
}
