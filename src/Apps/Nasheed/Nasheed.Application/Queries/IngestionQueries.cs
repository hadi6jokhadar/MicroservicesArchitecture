using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Enums;

namespace Nasheed.Application.Queries;

public record GetIngestionJobByIdQuery(int Id) : IRequest<IngestionJobDto?>;

public record GetIngestionJobListQuery(
    int? SongId = null,
    IngestionJobStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PaginatedList<IngestionJobDto>>;

public record GetSongAnalysisStatusQuery(int SongId) : IRequest<SongDto?>;
