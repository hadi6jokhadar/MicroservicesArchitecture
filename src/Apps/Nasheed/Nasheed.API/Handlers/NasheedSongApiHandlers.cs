using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;

namespace Nasheed.API.Handlers;

public static class NasheedSongApiHandlers
{
    public static async Task<IResult> Create(
        [FromBody] CreateSongCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Results.Created($"/api/songs/{result.Id}", result);
    }

    public static async Task<IResult> GetById(int id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSongByIdQuery(id), ct);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }

    public static async Task<IResult> GetAll(
        [AsParameters] GetSongListQuery query,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Update(
        int id,
        [FromBody] UpdateSongCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command with { Id = id }, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Delete(int id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteSongCommand(id), ct);
        return Results.NoContent();
    }

    public static async Task<IResult> GetAnalysisStatus(int id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSongAnalysisStatusQuery(id), ct);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }
}
