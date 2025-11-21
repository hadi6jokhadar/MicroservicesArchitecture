using System.Security.Claims;
using Identity.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Handlers;

public static class UserApiHandlers
{
    /// <summary>
    /// Handle get user profile
    /// </summary>
    public static async Task<IResult> GetProfileHandler(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId(context);
        if (userId == 0)
        {
            return Results.Unauthorized();
        }

        var query = new GetUserProfileCommand(userId);
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle update user profile
    /// </summary>
    public static async Task<IResult> UpdateProfileHandler(
        UpdateProfileCommand command,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId(context);
        if (userId == 0)
        {
            return Results.Unauthorized();
        }

        // Ensure the command's Id matches the authenticated user's ID
        command = command with { Id = userId };

        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle delete user account
    /// </summary>
    public static async Task<IResult> DeleteUserHandler(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId(context);
        if (userId == 0)
        {
            return Results.Unauthorized();
        }
        
        var command = new DeleteUserCommand(userId);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Helper method to extract user ID from claims
    /// </summary>
    private static int GetCurrentUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }
}