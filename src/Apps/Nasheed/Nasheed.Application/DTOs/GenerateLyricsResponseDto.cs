namespace Nasheed.Application.DTOs;

public class GenerateLyricsResponseDto
{
    public string GeneratedLyrics { get; set; } = string.Empty;
    public string? Theme { get; set; }
    public string? Style { get; set; }
}
