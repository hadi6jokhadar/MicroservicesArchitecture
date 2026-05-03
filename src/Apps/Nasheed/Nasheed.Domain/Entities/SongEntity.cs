using IhsanDev.Shared.Kernel.Entities;
using Nasheed.Domain.Enums;

namespace Nasheed.Domain.Entities;

public class SongEntity : BaseEntity
{
    public int ArtistId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string FileId { get; private set; } = string.Empty;
    public int? DurationSeconds { get; private set; }
    public string? LanguageCode { get; private set; }
    public string? LyricsRaw { get; private set; }
    public string? LyricsVerifiedLrc { get; private set; }
    public string? LyricsPlainText { get; private set; }
    public string? Summary { get; private set; }
    public string? VocalStyle { get; private set; }
    public SongState SongState { get; private set; }
    public SearchIndexStatus SearchIndexStatus { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    // Navigation
    public ArtistEntity? Artist { get; private set; }

    private SongEntity() { }

    public static SongEntity Create(int artistId, string title, string fileId)
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
            LyricsRaw = lyricsRaw;
            LyricsVerifiedLrc = null;
            LyricsPlainText = null;
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

    public void UpdateTitle(string? title)
    {
        if (title != null) Title = title;
    }

    public void SetState(SongState state) => SongState = state;
    public void SetSearchIndexStatus(SearchIndexStatus status) => SearchIndexStatus = status;
    public void Publish() => PublishedAt = DateTime.UtcNow;
}
