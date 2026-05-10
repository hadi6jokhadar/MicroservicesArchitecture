using IhsanDev.Shared.Kernel.Entities;
using Nasheed.Domain.Enums;

namespace Nasheed.Domain.Entities;

public class SongEntity : BaseEntity
{
    public int? ArtistId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int FileId { get; private set; }
    public int? DurationSeconds { get; private set; }
    public string? LanguageCode { get; private set; }
    public string? LyricsRaw { get; private set; }
    public string? LyricsVerifiedLrc { get; private set; }
    public string? LyricsPlainText { get; private set; }
    public string? Summary { get; private set; }
    public string? VocalStyle { get; private set; }
    public LegalComplianceEntity? LegalCompliance { get; private set; }
    public SongState SongState { get; private set; }
    public SearchIndexStatus SearchIndexStatus { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    // Navigation
    public ArtistEntity? Artist { get; private set; }
    public ICollection<SongMoodTagEntity>? MoodTags { get; private set; }

    private SongEntity() { }

    public static SongEntity Create(int? artistId, string title, int fileId)
    {
        return new SongEntity
        {
            ArtistId = artistId,
            Title = title,
            FileId = fileId,
            SongState = SongState.Uploaded,
            SearchIndexStatus = SearchIndexStatus.NotIndexed
        };
    }

    public void UpdateMetadata(
        string? languageCode,
        string? lyricsRaw,
        string? summary,
        string? vocalStyle,
        int? durationSeconds)
    {
        if (languageCode != null) LanguageCode = languageCode;
        if (lyricsRaw != null)
        {
            var rawChanged = !string.Equals(LyricsRaw, lyricsRaw, StringComparison.Ordinal);
            LyricsRaw = lyricsRaw;

            if (rawChanged)
            {
                LyricsVerifiedLrc = null;
                LyricsPlainText = null;
            }
        }
        if (summary != null) Summary = summary;
        if (vocalStyle != null) VocalStyle = vocalStyle;
        if (durationSeconds.HasValue) DurationSeconds = durationSeconds;
    }

    public void SetVerifiedLyrics(string lyricsVerifiedLrc, string lyricsPlainText)
    {
        LyricsVerifiedLrc = lyricsVerifiedLrc;
        LyricsPlainText = lyricsPlainText;
    }

    public void UpdateVerifiedLyrics(string? lyricsVerifiedLrc, string? lyricsPlainText)
    {
        if (lyricsVerifiedLrc != null)
        {
            LyricsVerifiedLrc = lyricsVerifiedLrc;
        }

        if (lyricsPlainText != null)
        {
            LyricsPlainText = lyricsPlainText;
        }
    }

    public void UpdateTitle(string? title)
    {
        if (title != null) Title = title;
    }

    public void UpdateArtist(int? artistId)
    {
        ArtistId = artistId;
    }

    public void UpdateLegalComplianceFromAi(string? copyrightRiskLevel, string? contentSafetyFlag, string? riskReason)
    {
        var normalizedRiskLevel = Normalize(copyrightRiskLevel);
        var normalizedSafetyFlag = Normalize(contentSafetyFlag);

        if (!LegalComplianceEntity.IsValidRiskLevel(normalizedRiskLevel) || !LegalComplianceEntity.IsValidSafetyFlag(normalizedSafetyFlag))
        {
            return;
        }

        var normalizedReason = string.IsNullOrWhiteSpace(riskReason) ? null : riskReason.Trim();

        if (LegalCompliance == null)
        {
            LegalCompliance = LegalComplianceEntity.Create(normalizedRiskLevel!, normalizedSafetyFlag!, normalizedReason);
            return;
        }

        LegalCompliance.Update(normalizedRiskLevel!, normalizedSafetyFlag!, normalizedReason);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant();
    }

    public void SetState(SongState state) => SongState = state;
    public void SetSearchIndexStatus(SearchIndexStatus status) => SearchIndexStatus = status;
    public void Publish() => PublishedAt = DateTime.UtcNow;
}
