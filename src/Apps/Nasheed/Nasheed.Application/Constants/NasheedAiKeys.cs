namespace Nasheed.Application.Constants;

/// <summary>
/// Hardcoded keys used to resolve AI provider settings and system prompts from AI.API.
/// These keys must exist in AI.API's database for each tenant scope (or have a global fallback).
/// </summary>
public static class NasheedAiKeys
{
    public const string ExtractionSettings    = "nasheed:extraction:settings";
    public const string ExtractionPrompt      = "nasheed:extraction:system-prompt";
    public const string VerificationSettings  = "nasheed:verification:settings";
    public const string VerificationPrompt    = "nasheed:verification:system-prompt";
    public const string GenerationSettings    = "nasheed:generation:settings";
    public const string GenerationPrompt      = "nasheed:generation:system-prompt";
    public const string EmbeddingSettings     = "nasheed:embedding:settings";
}
