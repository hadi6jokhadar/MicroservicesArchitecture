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

    /// <summary>
    /// When true, FileManager's SaveFileCommandHandler automatically uploads every newly
    /// saved local file to the tenant's configured external blob storage (e.g. Cloudflare
    /// R2) right after the local save succeeds — no manual "Upload to External" call needed.
    /// Defaults to false (manual upload-to-blob only) until a tenant explicitly opts in.
    /// See MicroservicesArchitecture/Doc/FILE_MANAGER.md's "Blob Storage" section.
    /// </summary>
    public const string AutoUploadToExternalStorageEnabled = "autoUploadToExternalStorageEnabled";
}
