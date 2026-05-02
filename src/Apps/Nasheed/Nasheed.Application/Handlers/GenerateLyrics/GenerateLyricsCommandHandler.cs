using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.Constants;
using Nasheed.Application.DTOs;
using Nasheed.Application.Interfaces;

namespace Nasheed.Application.Handlers.GenerateLyrics;

public class GenerateLyricsCommandHandler : IRequestHandler<GenerateLyricsCommand, GenerateLyricsResponseDto>
{
    private readonly IAiApiClient _aiClient;
    private readonly ILogger<GenerateLyricsCommandHandler> _logger;

    public GenerateLyricsCommandHandler(IAiApiClient aiClient, ILogger<GenerateLyricsCommandHandler> logger)
    {
        _aiClient = aiClient;
        _logger = logger;
    }

    public async Task<GenerateLyricsResponseDto> Handle(GenerateLyricsCommand request, CancellationToken cancellationToken)
    {
        var userMessage = BuildUserMessage(request);

        var response = await _aiClient.ChatAsync(
            NasheedAiKeys.GenerationSettings,
            NasheedAiKeys.GenerationPrompt,
            userMessage,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Generated lyrics for theme '{Theme}'", request.Theme);

        return new GenerateLyricsResponseDto
        {
            GeneratedLyrics = response,
            Theme = request.Theme,
            Style = request.Style
        };
    }

    private static string BuildUserMessage(GenerateLyricsCommand request)
    {
        var parts = new List<string> { $"Theme: {request.Theme}" };
        if (!string.IsNullOrWhiteSpace(request.Style))
            parts.Add($"Style: {request.Style}");
        if (!string.IsNullOrWhiteSpace(request.LanguageCode))
            parts.Add($"Language: {request.LanguageCode}");
        return string.Join("\n", parts);
    }
}
