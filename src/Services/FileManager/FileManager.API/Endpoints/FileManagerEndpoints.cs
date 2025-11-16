using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FileManager.API.Endpoints;

public static class FileManagerEndpoints
{
    public static IEndpointRouteBuilder MapFileManagerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/filemanager")
            .WithTags("FileManager");

        // Save file (Upload)
        group.MapPost("/files", [Authorize] async (
            [FromForm] IFormFile file,
            [FromForm] int? group,
            [FromForm] int? userId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            if (group == null){
                group = 1;
            }
            var command = new SaveFileCommand(file, (Domain.Enums.FileGroup)group, userId);
            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/filemanager/files/{result.Id}", result);
        })
        .WithName("SaveFile")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<FileManagerResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .DisableAntiforgery();

        // Get file by ID
        group.MapGet("/files/{id:int}", [Authorize] async (
            int id,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new GetFileByIdQuery(id);
            var result = await mediator.Send(query, cancellationToken);
            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetFileById")
        .Produces<FileManagerResponse>()
        .Produces(StatusCodes.Status404NotFound);

        // Get files list (with filters and pagination)
        group.MapGet("/files", [Authorize] async (
            [AsParameters] FileManagerListRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new GetFilesQuery(request);
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetFiles")
        .Produces<PaginatedList<FileManagerResponse>>();

        // Update file metadata
        group.MapPut("/files/{id:int}", [Authorize] async (
            int id,
            [FromBody] UpdateFileRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateFileCommand(id, request.Name, request.Group, request.Status, request.IsArchived, request.Temp);
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdateFile")
        .Produces<FileManagerResponse>()
        .Produces(StatusCodes.Status404NotFound);

        // Delete file
        group.MapDelete("/files/{id:int}", [Authorize] async (
            int id,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteFileCommand(id);
            var result = await mediator.Send(command, cancellationToken);
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteFile")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // Delete all temp files
        group.MapDelete("/files/temp/all", [Authorize(Roles = "Admin,SuperAdmin")] async (
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteAllTempFilesCommand();
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(new { DeletedCount = result });
        })
        .WithName("DeleteAllTempFiles")
        .Produces<object>();

        // Delete old temp files
        group.MapDelete("/files/temp/old", [Authorize(Roles = "Admin,SuperAdmin")] async (
            IMediator mediator,
            CancellationToken cancellationToken,
            [FromQuery] int olderThanDays = 7) =>
        {
            var command = new DeleteOldTempFilesCommand(olderThanDays);
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(new { DeletedCount = result, OlderThanDays = olderThanDays });
        })
        .WithName("DeleteOldTempFiles")
        .Produces<object>();

        // Download file by ID
        group.MapGet("/files/{id:int}/download", async (
            int id,
            IMediator mediator,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var query = new GetFileByIdQuery(id);
            var fileMetadata = await mediator.Send(query, cancellationToken);
            
            if (fileMetadata == null)
                return Results.NotFound();

            // Construct physical path from storage root + relative path
            var storageRoot = configuration["FileManagerOptions:FilesSavePath"] ?? "C:/FileStorage";
            var physicalPath = Path.Combine(storageRoot, fileMetadata.Path.Replace("/", "\\"));
            
            if (!File.Exists(physicalPath))
                return Results.NotFound(new { error = "File not found on disk", path = physicalPath });

            var fileStream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var contentType = GetContentType(fileMetadata.Extension);
            var fileName = $"{fileMetadata.Name}{fileMetadata.Extension}";

            return Results.File(fileStream, contentType, fileName, enableRangeProcessing: true);
        })
        .WithName("DownloadFile")
        .Produces<FileStreamResult>()
        .Produces(StatusCodes.Status404NotFound)
        .AllowAnonymous(); // Allow public access to download files

        return app;
    }

    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            ".mov" => "video/quicktime",
            _ => "application/octet-stream"
        };
    }
}

public record UpdateFileRequest(
    string? Name = null,
    Domain.Enums.FileGroup? Group = null,
    bool? Status = null,
    bool? IsArchived = null,
    bool? Temp = null
);
