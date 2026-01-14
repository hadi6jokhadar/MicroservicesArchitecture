using Identity.Application.Commands.Admin.Role;
using Identity.Application.Commands.Admin.Claim;
using Identity.Application.Queries.Role;
using Identity.Application.Queries.Claim;
using Identity.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Handlers;

public static class RoleApiHandlers
{
    /// <summary>
    /// Get all roles
    /// </summary>
    public static async Task<IResult> GetRolesHandler(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var roles = await mediator.Send(new GetRolesQuery(), cancellationToken);
        return Results.Ok(roles);
    }

    /// <summary>
    /// Get role by ID
    /// </summary>
    public static async Task<IResult> GetRoleByIdHandler(
        [FromRoute] int id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var role = await mediator.Send(new GetRoleByIdQuery(id), cancellationToken);
        return Results.Ok(role);
    }

    /// <summary>
    /// Create new role
    /// </summary>
    public static async Task<IResult> CreateRoleHandler(
        CreateRoleCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var role = await mediator.Send(command, cancellationToken);
        return Results.Ok(role);
    }

    /// <summary>
    /// Update existing role
    /// </summary>
    public static async Task<IResult> UpdateRoleHandler(
        [FromRoute] int id,
        [FromBody] UpdateRoleRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRoleCommand(id, request.Name, request.Description);
        var role = await mediator.Send(command, cancellationToken);
        return Results.Ok(role);
    }

    /// <summary>
    /// Delete role
    /// </summary>
    public static async Task<IResult> DeleteRoleHandler(
        [FromRoute] int id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteRoleCommand(id), cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Assign claims to role
    /// </summary>
    public static async Task<IResult> AssignClaimsToRoleHandler(
        [FromRoute] int id,
        [FromBody] AssignClaimsRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AssignClaimsToRoleCommand(id, request.ClaimIds);
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Assign roles to user (replaces existing roles)
    /// </summary>
    public static async Task<IResult> AssignRolesToUserHandler(
        [FromRoute] int id,
        [FromBody] AssignRolesToUserRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AssignRolesToUserCommand(id, request.RoleIds);
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}

public static class ClaimApiHandlers
{
    /// <summary>
    /// Get all claims
    /// </summary>
    public static async Task<IResult> GetClaimsHandler(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var claims = await mediator.Send(new GetClaimsQuery(), cancellationToken);
        return Results.Ok(claims);
    }

    /// <summary>
    /// Get claim by ID
    /// </summary>
    public static async Task<IResult> GetClaimByIdHandler(
        [FromRoute] int id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var claim = await mediator.Send(new GetClaimByIdQuery(id), cancellationToken);
        return Results.Ok(claim);
    }

    /// <summary>
    /// Create new claim
    /// </summary>
    public static async Task<IResult> CreateClaimHandler(
        CreateClaimCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var claim = await mediator.Send(command, cancellationToken);
        return Results.Ok(claim);
    }

    /// <summary>
    /// Update existing claim
    /// </summary>
    public static async Task<IResult> UpdateClaimHandler(
        [FromRoute] int id,
        [FromBody] UpdateClaimRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateClaimCommand(
            id, 
            request.Name, 
            request.ClaimType, 
            request.ClaimValue, 
            request.IsSuperAdminOnly, 
            request.Description);
        var claim = await mediator.Send(command, cancellationToken);
        return Results.Ok(claim);
    }

    /// <summary>
    /// Delete claim
    /// </summary>
    public static async Task<IResult> DeleteClaimHandler(
        [FromRoute] int id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteClaimCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}

// Request DTOs for update operations
public record UpdateRoleRequest(string Name, string? Description);
public record UpdateClaimRequest(string Name, string ClaimType, string ClaimValue, bool IsSuperAdminOnly, string? Description);
public record AssignClaimsRequest(List<int> ClaimIds);
public record AssignRolesToUserRequest(List<int> RoleIds);
