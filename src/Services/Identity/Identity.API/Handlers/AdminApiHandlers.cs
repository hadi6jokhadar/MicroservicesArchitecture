using Identity.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Handlers;

public static class AdminApiHandlers
{
    /// <summary>
    /// Handle get all users (Admin only)
    /// </summary>
    public static async Task<IResult> GetUsersHandler(
        [AsParameters] GetUsersCommand query,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle get user by ID (Admin only)
    /// </summary>
    public static async Task<IResult> GetUserByIdHandler(
        int id,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var query = new GetUserByIdCommand(id);
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle create user (Admin only)
    /// </summary>
    public static async Task<IResult> CreateUserHandler(
        CreateUserCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle update user (Admin only)
    /// </summary>
    public static async Task<IResult> UpdateUserHandler(
        int id,
        UpdateUserCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle toggle user status (Admin only)
    /// </summary>
    public static async Task<IResult> ToggleUserStatusHandler(
        int id,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var command = new ToggleUserStatusCommand(id);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle delete user (Admin only)
    /// </summary>
    public static async Task<IResult> DeleteUserHandler(
        int id,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var command = new DeleteUserCommand(id);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle toggle user archived status (Admin only)
    /// </summary>
    public static async Task<IResult> ToggleUserArchivedStatusHandler(
        int id,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var command = new ToggleUserArchivedStatusCommand(id);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }
}