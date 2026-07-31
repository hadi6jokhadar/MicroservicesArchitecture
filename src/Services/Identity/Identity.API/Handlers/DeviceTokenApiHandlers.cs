using Identity.Application.Commands.DeviceToken;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Identity;
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
        [FromBody] AddDeviceTokenRequest request,
        ICurrentUserService currentUserService,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var userId = ResolveTargetUserId(currentUserService, request.UserId);

        var command = new AddDeviceTokenCommand(
            userId,
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
        ICurrentUserService currentUserService,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDeviceTokenByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
            return Results.NotFound(new { message = localizationService.GetString(LocalizationKeys.Exceptions.NotFound) });

        if (!IsPrivilegedCaller(currentUserService) && !OwnsResource(currentUserService, result.UserId))
            throw new ForbiddenException(LocalizationKeys.Exceptions.Forbidden);

        return Results.Ok(result);
    }

    /// <summary>
    /// Get all device tokens for a user
    /// </summary>
    public static async Task<IResult> GetUserDeviceTokens(
        int userId,
        ICurrentUserService currentUserService,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var resolvedUserId = ResolveTargetUserId(currentUserService, userId);
        var query = new GetUserDeviceTokensQuery(resolvedUserId);
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Get device tokens by user ID and platform
    /// </summary>
    public static async Task<IResult> GetUserDeviceTokensByPlatform(
        int userId,
        [FromQuery] Platform platform,
        ICurrentUserService currentUserService,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var resolvedUserId = ResolveTargetUserId(currentUserService, userId);
        var query = new GetUserDeviceTokensByPlatformQuery(resolvedUserId, platform);
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Update a device token
    /// </summary>
    public static async Task<IResult> UpdateDeviceToken(
        int id,
        [FromBody] UpdateDeviceTokenRequest request,
        ICurrentUserService currentUserService,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnershipOrPrivilegedAsync(id, currentUserService, mediator, cancellationToken);

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
        ICurrentUserService currentUserService,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnershipOrPrivilegedAsync(id, currentUserService, mediator, cancellationToken);

        var command = new DeleteDeviceTokenCommand(id);
        var result = await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// Delete all device tokens for a user
    /// </summary>
    public static async Task<IResult> DeleteAllUserDeviceTokens(
        int userId,
        ICurrentUserService currentUserService,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var resolvedUserId = ResolveTargetUserId(currentUserService, userId);
        var command = new DeleteAllUserDeviceTokensCommand(resolvedUserId);
        var result = await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// Callers with role Service/Admin/SuperAdmin may act on any user's device tokens (as the
    /// route already requires); a plain "User" caller can only ever act as themselves — the
    /// route parameter/body userId is ignored rather than trusted for that role.
    /// </summary>
    private static int ResolveTargetUserId(ICurrentUserService currentUserService, int requestedUserId)
    {
        if (IsPrivilegedCaller(currentUserService))
            return requestedUserId;

        return int.TryParse(currentUserService.UserId, out var ownUserId) ? ownUserId : 0;
    }

    private static bool OwnsResource(ICurrentUserService currentUserService, int resourceUserId)
    {
        return int.TryParse(currentUserService.UserId, out var ownUserId) && ownUserId == resourceUserId;
    }

    private static bool IsPrivilegedCaller(ICurrentUserService currentUserService)
    {
        return currentUserService.HasRole("Service") || currentUserService.HasRole("Admin") || currentUserService.HasRole("SuperAdmin");
    }

    /// <summary>
    /// For id-based routes (no userId in the URL/body to redirect), the owning user must be
    /// looked up before the mutation runs — otherwise a non-owner could still delete/update
    /// someone else's token before any check happens.
    /// </summary>
    private static async Task EnsureOwnershipOrPrivilegedAsync(
        int deviceTokenId,
        ICurrentUserService currentUserService,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (IsPrivilegedCaller(currentUserService))
            return;

        var existing = await mediator.Send(new GetDeviceTokenByIdQuery(deviceTokenId), cancellationToken);
        if (existing != null && !OwnsResource(currentUserService, existing.UserId))
            throw new ForbiddenException(LocalizationKeys.Exceptions.Forbidden);
    }

    /// <summary>
    /// Get device tokens for multiple users in batch (service-to-service only)
    /// </summary>
    public static async Task<IResult> GetBatchDeviceTokens(
        [FromBody] BatchUserIdsRequest request,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var query = new GetBatchDeviceTokensQuery(request.UserIds);
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Delete multiple device tokens in batch (service-to-service only)
    /// </summary>
    public static async Task<IResult> DeleteBatchDeviceTokens(
        [FromBody] BatchTokenIdsRequest request,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteBatchDeviceTokensCommand(request.TokenIds);
        var deletedCount = await mediator.Send(command, cancellationToken);
        return Results.Ok(deletedCount);
    }

    /// <summary>
    /// Get all device tokens (for global notifications - service-to-service only)
    /// </summary>
    public static async Task<IResult> GetAllDeviceTokens(
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllDeviceTokensQuery();
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Get all device tokens for current tenant (for tenant-wide notifications)
    /// </summary>
    public static async Task<IResult> GetTenantDeviceTokens(
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTenantDeviceTokensQuery();
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
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

public record BatchUserIdsRequest(List<int> UserIds);

public record BatchTokenIdsRequest(List<int> TokenIds);
