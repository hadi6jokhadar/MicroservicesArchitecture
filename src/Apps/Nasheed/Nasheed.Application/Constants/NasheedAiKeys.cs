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

    // "New" (hybrid) lyrics/timing extraction pipeline (feature-flagged —
    // FeatureFlags.NasheedNewLyricsExtractionEnabled). This pipeline gets its lyrics TEXT from the
    // same multimodal call as the old pipeline (ExtractionSettings/ExtractionPrompt, audio attached —
    // proven better text quality than a pure-ASR-plus-correction approach), and its lyrics TIMING
    // from TranscriptionSettings (a real ASR model, e.g. Whisper — must support word-level
    // timestamp_granularities). MergePrompt is a text-only, no-audio-needed prompt that aligns the
    // extraction call's trusted lyric lines against the ASR's indexed word list, reporting which word
    // index each line starts at — pure alignment, never rewriting the trusted text. EnrichmentPrompt
    // is a separate text-only prompt (no audio, no timestamp rules) that only produces
    // summary/vocal_style/mood_tags/legal_compliance/language_code from the ASR transcript — kept
    // independent of lyrics/timing work so it isn't sharing generation budget with audio processing.
    public const string TranscriptionSettings = "nasheed:transcription:settings";
    public const string MergeSettings         = "nasheed:merge:settings";
    public const string MergePrompt           = "nasheed:merge:system-prompt";
    public const string EnrichmentSettings    = "nasheed:enrichment:settings";
    public const string EnrichmentPrompt      = "nasheed:enrichment:system-prompt";
}
