using MediatR;
using Microsoft.AspNetCore.Mvc;
using PolySnap.Application.Commands;
using PolySnap.Application.DTOs;
using PolySnap.Application.Queries;

namespace PolySnap.API.Handlers;

public static class SnapRequestApiHandlers
{
    public static async Task<IResult> Create(
        [FromBody] CreateSnapRequestCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Results.Created($"/api/v1/snap-requests/{result.Id}", result);
    }

    public static async Task<IResult> GetById(int id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSnapRequestByIdQuery(id), ct);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }

    public static async Task<IResult> GetAll(
        [AsParameters] GetSnapRequestListQuery query,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Update(
        int id,
        [FromBody] UpdateSnapRequestCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command with { Id = id }, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Delete(int id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteSnapRequestCommand(id), ct);
        return Results.Ok();
    }
}
