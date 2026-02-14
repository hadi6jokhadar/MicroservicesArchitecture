using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Queries;
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
        CancellationToken ct)
    {
        if (group == null) group = 1;
        var command = new SaveFileCommand(file, (Domain.Enums.FileGroup)group, userId);
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
