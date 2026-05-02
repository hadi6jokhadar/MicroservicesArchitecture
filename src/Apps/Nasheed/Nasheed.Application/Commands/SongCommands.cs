using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Commands;

public record CreateSongCommand(
    int ArtistId,
    string Title,
    string FileId
) : IRequest<SongDto>;

public record UpdateSongCommand(
    int Id,
    string? Title,
    int? ArtistId
) : IRequest<SongDto>;

public record DeleteSongCommand(int Id) : IRequest<bool>;
