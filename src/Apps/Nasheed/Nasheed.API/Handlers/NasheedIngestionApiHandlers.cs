using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;

namespace Nasheed.API.Handlers;

public static class NasheedIngestionApiHandlers
{
    public static async Task<IResult> GetById(int id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetIngestionJobByIdQuery(id), ct);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }

    public static async Task<IResult> GetAll(
        [AsParameters] GetIngestionJobListQuery query,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Retry(int id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new RetryIngestionJobCommand(id), ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Remove(int id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new RemoveIngestionJobCommand(id), ct);
        return Results.NoContent();
    }

    public static async Task<IResult> Reindex(int songId, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new ReindexSongCommand(songId), ct);
        return Results.Ok(result);
    }
}
