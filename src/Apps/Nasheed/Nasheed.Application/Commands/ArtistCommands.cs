using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Commands;

public record CreateArtistCommand(
    string Name,
    string? ImageFileId = null
) : IRequest<ArtistDto>;

public record UpdateArtistCommand(
    int Id,
    string? Name,
    string? ImageFileId
) : IRequest<ArtistDto>;

public record DeleteArtistCommand(int Id) : IRequest<bool>;
