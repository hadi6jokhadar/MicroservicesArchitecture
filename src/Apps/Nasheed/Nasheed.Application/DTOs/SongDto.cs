using System.Globalization;
using IhsanDev.Shared.Kernel.Dto.Identity;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;

namespace Nasheed.Application.DTOs;

public class SongDto : BaseDto
{
    public int ArtistId { get; set; }
    public string? ArtistName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public string? LanguageCode { get; set; }
    public string? LyricsRaw { get; set; }
    public string? LyricsVerifiedLrc { get; set; }
    public string? LyricsPlainText { get; set; }
    public string? Summary { get; set; }
    public string? VocalStyle { get; set; }
    public SongState SongState { get; set; }
    public SearchIndexStatus SearchIndexStatus { get; set; }
    public string? PublishedAt { get; set; }
    public List<string> MoodTags { get; set; } = new();

    public static SongDto MapFrom(SongEntity entity, List<string>? moodTags = null) => new()
    {
        Id = entity.Id,
        ArtistId = entity.ArtistId,
        ArtistName = entity.Artist?.Name,
        Title = entity.Title,
        FileId = entity.FileId,
        DurationSeconds = entity.DurationSeconds,
        LanguageCode = entity.LanguageCode,
        LyricsRaw = entity.LyricsRaw,
        LyricsVerifiedLrc = entity.LyricsVerifiedLrc,
        LyricsPlainText = entity.LyricsPlainText,
        Summary = entity.Summary,
        VocalStyle = entity.VocalStyle,
        SongState = entity.SongState,
        SearchIndexStatus = entity.SearchIndexStatus,
        PublishedAt = entity.PublishedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        MoodTags = moodTags ?? new List<string>(),
        Status = entity.Status,
        IsArchived = entity.IsArchived,
        Created = entity.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        LastModified = entity.LastModified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };
}
