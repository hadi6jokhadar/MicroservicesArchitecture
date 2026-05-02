using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Commands;

public record GenerateLyricsCommand(
    string Theme,
    string? Style = null,
    string? LanguageCode = null
) : IRequest<GenerateLyricsResponseDto>;
