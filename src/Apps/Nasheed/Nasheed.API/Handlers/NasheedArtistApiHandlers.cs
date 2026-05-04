using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;

namespace Nasheed.API.Handlers;

public static class NasheedArtistApiHandlers
{
    public static async Task<IResult> Create(
        [FromBody] CreateArtistCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Results.Created($"/api/artists/{result.Id}", result);
    }

    public static async Task<IResult> GetById(int id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetArtistByIdQuery(id), ct);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }

    public static async Task<IResult> GetAll(
        [AsParameters] GetArtistListQuery query,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Update(
        int id,
        [FromBody] UpdateArtistCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command with { Id = id }, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Delete(int id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteArtistCommand(id), ct);
        return Results.Ok();
    }
}
