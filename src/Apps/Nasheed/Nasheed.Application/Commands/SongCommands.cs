using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Commands;

public record CreateSongCommand(
    int? ArtistId,
    string Title,
    int FileId,
    string? CopyrightRiskLevel = null,
    string? ContentSafetyFlag = null,
    string? RiskReason = null
) : IRequest<SongDto>;

public record UpdateSongCommand(
    int Id,
    string? Title,
    int? ArtistId,
    string? CopyrightRiskLevel = null,
    string? ContentSafetyFlag = null,
    string? RiskReason = null
) : IRequest<SongDto>;

public record DeleteSongCommand(int Id) : IRequest<bool>;
