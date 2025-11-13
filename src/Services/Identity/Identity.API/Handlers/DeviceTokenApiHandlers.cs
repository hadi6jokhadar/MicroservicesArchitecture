using Identity.Application.Commands.DeviceToken;
using IhsanDev.Shared.Kernel.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Handlers;

public static class DeviceTokenApiHandlers
{
    /// <summary>
    /// Add a new device token
    /// </summary>
    public static async Task<IResult> AddDeviceToken(
        AddDeviceTokenRequest request,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var command = new AddDeviceTokenCommand(
            request.UserId,
            request.Token,
            request.Platform,
            request.DeviceIdentifier,
            request.IsPrimary);

        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/device-tokens/{result.Id}", result);
    }

    /// <summary>
    /// Get device token by ID
    /// </summary>
    public static async Task<IResult> GetDeviceTokenById(
        int id,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDeviceTokenByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        return result == null
            ? Results.NotFound($"Device token with ID {id} not found")
            : Results.Ok(result);
    }

    /// <summary>
    /// Get all device tokens for a user
    /// </summary>
    public static async Task<IResult> GetUserDeviceTokens(
        int userId,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserDeviceTokensQuery(userId);
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Get device tokens by user ID and platform
    /// </summary>
    public static async Task<IResult> GetUserDeviceTokensByPlatform(
        int userId,
        [FromQuery] Platform platform,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserDeviceTokensByPlatformQuery(userId, platform);
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Update a device token
    /// </summary>
    public static async Task<IResult> UpdateDeviceToken(
        int id,
        UpdateDeviceTokenRequest request,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateDeviceTokenCommand(
            id,
            request.Token,
            request.DeviceIdentifier,
            request.IsPrimary);

        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Delete a device token by ID
    /// </summary>
    public static async Task<IResult> DeleteDeviceToken(
        int id,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteDeviceTokenCommand(id);
        var result = await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// Delete all device tokens for a user
    /// </summary>
    public static async Task<IResult> DeleteAllUserDeviceTokens(
        int userId,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteAllUserDeviceTokensCommand(userId);
        var result = await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}

// Request DTOs
public record AddDeviceTokenRequest(
    int UserId,
    string Token,
    Platform Platform,
    string? DeviceIdentifier = null,
    bool IsPrimary = false);

public record UpdateDeviceTokenRequest(
    string? Token = null,
    string? DeviceIdentifier = null,
    bool? IsPrimary = null);
