using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Nasheed.Application.Commands;
using Nasheed.Application.Constants;
using Nasheed.Application.DTOs;
using Nasheed.Application.Interfaces;
using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Application.Constants;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Exceptions;

namespace Nasheed.Application.Handlers.GenerateLyrics;

public class GenerateLyricsCommandHandler : IRequestHandler<GenerateLyricsCommand, GenerateLyricsResponseDto>
{
    private readonly IAiApiClient _aiClient;
    private readonly ILogger<GenerateLyricsCommandHandler> _logger;
    private readonly IFeatureFlagService _featureFlags;

    public GenerateLyricsCommandHandler(
        IAiApiClient aiClient,
        ILogger<GenerateLyricsCommandHandler> logger,
        IFeatureFlagService featureFlags)
    {
        _aiClient = aiClient;
        _logger = logger;
        _featureFlags = featureFlags;
    }

    public async Task<GenerateLyricsResponseDto> Handle(GenerateLyricsCommand request, CancellationToken cancellationToken)
    {
        if (!_featureFlags.IsEnabled(FeatureFlags.AiChatEnabled, defaultValue: true))
            throw new ForbiddenException(LocalizationKeys.Exceptions.FeatureNotEnabled);

        var userMessage = BuildUserMessage(request);

        string response;
        try
        {
            response = await _aiClient.ChatAsync(
                NasheedAiKeys.ExtractionSettings,
                NasheedAiKeys.ExtractionPrompt,
                userMessage,
                cancellationToken: cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "AI circuit open; lyrics generation unavailable for theme '{Theme}'", request.Theme);
            throw;
        }

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
