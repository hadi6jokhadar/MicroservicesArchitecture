using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;

namespace Nasheed.API.Handlers;

public static class NasheedInteractionApiHandlers
{
    public static async Task<IResult> AddFavorite(
        int songId,
        [FromBody] AddFavoriteRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new AddFavoriteCommand(songId, request.UserId), ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> RemoveFavorite(
        int songId,
        [FromBody] RemoveFavoriteRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new RemoveFavoriteCommand(songId, request.UserId), ct);
        return Results.NoContent();
    }

    public static async Task<IResult> AddRating(
        int songId,
        [FromBody] AddRatingCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command with { SongId = songId }, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> LogPlay(
        int songId,
        [FromBody] AddPlayLogRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new AddPlayLogCommand(songId, request.UserId), ct);
        return Results.NoContent();
    }
}

public record AddFavoriteRequest(string UserId);
public record RemoveFavoriteRequest(string UserId);
public record AddPlayLogRequest(string UserId);
