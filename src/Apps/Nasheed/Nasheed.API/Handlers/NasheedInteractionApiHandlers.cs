using IhsanDev.Shared.Infrastructure.Services.Identity;
using MediatR;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;

namespace Nasheed.API.Handlers;

public static class NasheedInteractionApiHandlers
{
    public static async Task<IResult> AddFavorite(
        int songId,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (!TryGetUserId(currentUser, out var userId))
            return Results.Unauthorized();

        var result = await mediator.Send(new AddFavoriteCommand(songId, userId), ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> RemoveFavorite(
        int songId,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (!TryGetUserId(currentUser, out var userId))
            return Results.Unauthorized();

        await mediator.Send(new RemoveFavoriteCommand(songId, userId), ct);
        return Results.Ok();
    }

    public static async Task<IResult> AddRating(
        int songId,
        [Microsoft.AspNetCore.Mvc.FromBody] AddRatingCommand command,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (!TryGetUserId(currentUser, out var userId))
            return Results.Unauthorized();

        var result = await mediator.Send(command with { SongId = songId, UserId = userId }, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> LogPlay(
        int songId,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (!TryGetUserId(currentUser, out var userId))
            return Results.Unauthorized();

        await mediator.Send(new AddPlayLogCommand(songId, userId), ct);
        return Results.Ok();
    }

    private static bool TryGetUserId(ICurrentUserService currentUser, out int userId)
    {
        userId = 0;
        return currentUser.UserId != null && int.TryParse(currentUser.UserId, out userId) && userId > 0;
    }
}
