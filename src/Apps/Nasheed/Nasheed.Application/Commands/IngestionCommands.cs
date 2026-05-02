using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Commands;

public record RetryIngestionJobCommand(int JobId) : IRequest<IngestionJobDto>;
public record RemoveIngestionJobCommand(int JobId) : IRequest<bool>;
public record ReindexSongCommand(int SongId) : IRequest<IngestionJobDto>;
