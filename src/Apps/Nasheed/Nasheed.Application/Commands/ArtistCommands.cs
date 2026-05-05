using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Commands;

public record CreateArtistCommand(
    string Name,
    int? ImageFileId = null
) : IRequest<ArtistDto>;

public record UpdateArtistCommand(
    int Id,
    string? Name,
    int? ImageFileId
) : IRequest<ArtistDto>;

public record DeleteArtistCommand(int Id) : IRequest<bool>;
