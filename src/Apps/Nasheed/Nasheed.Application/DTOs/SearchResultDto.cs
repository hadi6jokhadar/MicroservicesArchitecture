namespace Nasheed.Application.DTOs;

public class SearchResultDto
{
    public int SongId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public string? Summary { get; set; }
    public string? VocalStyle { get; set; }
    public List<string> MoodTags { get; set; } = new();
    public double Score { get; set; }
}
