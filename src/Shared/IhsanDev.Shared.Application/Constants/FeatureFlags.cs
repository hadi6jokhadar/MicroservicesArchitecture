namespace IhsanDev.Shared.Application.Constants;

public static class FeatureFlags
{
    public const string AiChatEnabled = "aiChatEnabled";
    public const string NasheedIngestionEnabled = "nasheedIngestionEnabled";
    public const string IsBackgroundJobPageEnabled = "isBackgroundJobPageEnabled";
    public const string IsAuditLogPageEnabled = "isAuditLogPageEnabled";

    /// <summary>
    /// When true, Nasheed's ingestion worker extracts lyrics/timing via a dedicated
    /// ASR transcription call (real audio alignment) instead of asking the chat model
    /// to generate LRC timestamps in one shot. Defaults to false (old flow) until a
    /// tenant explicitly opts in. See Nasheed's Doc/AI_INTEGRATION.md.
    /// </summary>
    public const string NasheedNewLyricsExtractionEnabled = "nasheedNewLyricsExtractionEnabled";
}
