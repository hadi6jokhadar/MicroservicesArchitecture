using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Enums;

namespace Nasheed.Application.Queries;

public record GetSongByIdQuery(int Id) : IRequest<SongDto?>;

public record GetSongListQuery(
    string? TextFilter = null,
    int? ArtistId = null,
    SongState? State = null,
    string? CopyrightRiskLevel = null,
    string? ContentSafetyFlag = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PaginatedList<SongDto>>;
