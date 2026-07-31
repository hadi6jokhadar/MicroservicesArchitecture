using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Queries;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FileManager.API.Handlers;

public static class FileManagerApiHandlers
{
    public static async Task<IResult> SaveFile(
        IFormFile file,
        [FromForm] int? group,
        [FromForm] int? userId,
        IMediator mediator,
        ICurrentUserService currentUserService,
        CancellationToken ct)
    {
        if (group == null) group = 1;

        // The uploader's identity must come from the authenticated caller, not the client-supplied
        // form field — otherwise any caller can tag an upload as belonging to a different user.
        // Service/Admin/SuperAdmin callers keep the ability to upload on behalf of another user
        // (service-to-service uploads); a plain User-role caller always gets their own JWT-derived ID.
        var isPrivilegedCaller = currentUserService.HasRole("Service")
            || currentUserService.HasRole("Admin")
            || currentUserService.IsSuperAdmin;

        var effectiveUserId = isPrivilegedCaller
            ? userId
            : (int.TryParse(currentUserService.UserId, out var authenticatedUserId) ? authenticatedUserId : null);

        var command = new SaveFileCommand(file, (Domain.Enums.FileGroup)group, effectiveUserId);
        var result = await mediator.Send(command, ct);
        return Results.Created($"/api/filemanager/files/{result.Id}", result);
    }

    public static async Task<IResult> GetFileById(
        int id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetFileByIdQuery(id);
        var result = await mediator.Send(query, ct);
        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    public static async Task<IResult> GetFiles(
        [AsParameters] FileManagerListRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetFilesQuery(request);
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> UpdateFile(
        int id,
        [FromBody] UpdateFileRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateFileCommand(id, request.Name, request.Group, request.Status, request.IsArchived, request.Temp);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> DeleteFile(
        int id,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new DeleteFileCommand(id);
        var result = await mediator.Send(command, ct);
        return result ? Results.NoContent() : Results.NotFound();
    }
}
