using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Queries;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Attributes;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FileManager.API.Endpoints;

public static class FileManagerEndpoints
{
    /// <summary>
    /// Helper method to set tenant context in a new scope before resolving dependencies
    /// This prevents the "first request returns null" issue where DbContext is configured before tenant context is set
    /// </summary>
    private static async Task<(IServiceScope scope, ITenantContext? tenantContext)> CreateScopeWithTenantAsync(
        IServiceProvider serviceProvider,
        string? tenantId,
        ITenantConfigurationProvider tenantConfigProvider,
        CancellationToken cancellationToken)
    {
        // Create new scope for fresh DbContext
        var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var tenantContext = scopedServices.GetRequiredService<ITenantContext>();

        // Set tenant context if tenantId provided
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var tenant = await tenantConfigProvider.GetTenantConfigurationAsync(tenantId, cancellationToken);
            if (tenant != null)
            {
                tenantContext.SetTenant(tenant);
            }
        }

        return (scope, tenantContext);
    }

    public static IEndpointRouteBuilder MapFileManagerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/filemanager")
            .WithTags("FileManager");

        // ============================================
        // TENANT USER ENDPOINTS (require x-tenant-id)
        // ============================================
        
        // Save file (Upload) - Tenant Users
        group.MapPost("/files", async (
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
        .RequireAuthorization(policy => policy.RequireRole("User", "Admin", "SuperAdmin"))
        .WithName("SaveFile")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<FileManagerResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .DisableAntiforgery();

        // ============================================
        // GLOBAL ADMIN ENDPOINTS (no x-tenant-id required)
        // ============================================

        var adminGroup = app.MapGroup("/api/filemanager/admin")
            .WithTags("FileManager - Admin");

        // Save file (Upload) - Global Users (SuperAdmin/Service)
        // Optional tenantId query parameter: if provided, saves to that tenant's database; otherwise saves to global database
        adminGroup.MapPost("/files", async (
            [FromForm] IFormFile file,
            [FromForm] int? group,
            [FromForm] int? userId,
            [FromQuery] string? tenantId,
            ITenantConfigurationProvider tenantConfigProvider,
            ILocalizationService localizationService,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
        {
            // Create scope with tenant context set before resolving dependencies
            var scopeResult = await CreateScopeWithTenantAsync(
                serviceProvider, tenantId, tenantConfigProvider, cancellationToken);
            
            using var scope = scopeResult.scope;
            var tenantContext = scopeResult.tenantContext;

            if (!string.IsNullOrWhiteSpace(tenantId) && tenantContext?.CurrentTenant == null)
            {
                return Results.NotFound(new 
                { 
                    error = localizationService.GetString(LocalizationKeys.Exceptions.TenantNotFound),
                    message = $"Tenant '{tenantId}' not found"
                });
            }

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            
            if (group == null){
                group = 1;
            }
            var command = new SaveFileCommand(file, (Domain.Enums.FileGroup)group, userId);
            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/filemanager/admin/files/{result.Id}", result);
        })
        .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
        .WithName("SaveFileAdmin")
        .WithMetadata(new BypassTenantAttribute())
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<FileManagerResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        // Get file by ID - Tenant Users
        group.MapGet("/files/{id:int}", async (
            int id,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new GetFileByIdQuery(id);
            var result = await mediator.Send(query, cancellationToken);
            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization(policy => policy.RequireRole("User", "Admin", "SuperAdmin"))
        .WithName("GetFileById")
        .Produces<FileManagerResponse>()
        .Produces(StatusCodes.Status404NotFound);

        // Get file by ID - Global Users
        // Optional tenantId query parameter: if provided, reads from that tenant's database; otherwise reads from global database
        adminGroup.MapGet("/files/{id:int}", async (
            int id,
            [FromQuery] string? tenantId,
            ITenantConfigurationProvider tenantConfigProvider,
            ILocalizationService localizationService,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await CreateScopeWithTenantAsync(
                serviceProvider, tenantId, tenantConfigProvider, cancellationToken);
            
            using var scope = scopeResult.scope;
            var tenantContext = scopeResult.tenantContext;

            if (!string.IsNullOrWhiteSpace(tenantId) && tenantContext?.CurrentTenant == null)
            {
                return Results.NotFound(new 
                { 
                    error = localizationService.GetString(LocalizationKeys.Exceptions.TenantNotFound),
                    message = $"Tenant '{tenantId}' not found" 
                });
            }

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var query = new GetFileByIdQuery(id);
            var result = await mediator.Send(query, cancellationToken);
            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
        .WithName("GetFileByIdAdmin")
        .WithMetadata(new BypassTenantAttribute())
        .Produces<FileManagerResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        // Get files list (with filters and pagination) - Tenant Users
        group.MapGet("/files", async (
            [AsParameters] FileManagerListRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new GetFilesQuery(request);
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireRole("User", "Admin", "SuperAdmin"))
        .WithName("GetFiles")
        .Produces<PaginatedList<FileManagerResponse>>();

        // Get files list - Global Users
        // Optional tenantId query parameter: if provided, reads from that tenant's database; otherwise reads from global database
        adminGroup.MapGet("/files", async (
            [AsParameters] FileManagerListRequest request,
            [FromQuery] string? tenantId,
            ITenantConfigurationProvider tenantConfigProvider,
            ILocalizationService localizationService,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await CreateScopeWithTenantAsync(
                serviceProvider, tenantId, tenantConfigProvider, cancellationToken);
            
            using var scope = scopeResult.scope;
            var tenantContext = scopeResult.tenantContext;

            if (!string.IsNullOrWhiteSpace(tenantId) && tenantContext?.CurrentTenant == null)
            {
                return Results.NotFound(new 
                { 
                    error = localizationService.GetString(LocalizationKeys.Exceptions.TenantNotFound),
                    message = $"Tenant '{tenantId}' not found" 
                });
            }

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var query = new GetFilesQuery(request);
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
        .WithName("GetFilesAdmin")
        .WithMetadata(new BypassTenantAttribute())
        .Produces<PaginatedList<FileManagerResponse>>()
        .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        // Update file metadata - Tenant Users
        group.MapPut("/files/{id:int}", async (
            int id,
            [FromBody] UpdateFileRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateFileCommand(id, request.Name, request.Group, request.Status, request.IsArchived, request.Temp);
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireRole("User", "Admin", "SuperAdmin"))
        .WithName("UpdateFile")
        .Produces<FileManagerResponse>()
        .Produces(StatusCodes.Status404NotFound);

        // Update file metadata - Global Users
        // Optional tenantId query parameter: if provided, updates in that tenant's database; otherwise updates in global database
        adminGroup.MapPut("/files/{id:int}", async (
            int id,
            [FromBody] UpdateFileRequest request,
            [FromQuery] string? tenantId,
            ITenantConfigurationProvider tenantConfigProvider,
            ILocalizationService localizationService,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await CreateScopeWithTenantAsync(
                serviceProvider, tenantId, tenantConfigProvider, cancellationToken);
            
            using var scope = scopeResult.scope;
            var tenantContext = scopeResult.tenantContext;

            if (!string.IsNullOrWhiteSpace(tenantId) && tenantContext?.CurrentTenant == null)
            {
                return Results.NotFound(new 
                { 
                    error = localizationService.GetString(LocalizationKeys.Exceptions.TenantNotFound),
                    message = $"Tenant '{tenantId}' not found" 
                });
            }

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var command = new UpdateFileCommand(id, request.Name, request.Group, request.Status, request.IsArchived, request.Temp);
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
        .WithName("UpdateFileAdmin")
        .WithMetadata(new BypassTenantAttribute())
        .Produces<FileManagerResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        // Delete file - Tenant Users
        group.MapDelete("/files/{id:int}", async (
            int id,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteFileCommand(id);
            var result = await mediator.Send(command, cancellationToken);
            return result ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization(policy => policy.RequireRole("User", "Admin", "SuperAdmin"))
        .WithName("DeleteFile")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // Delete file - Global Users
        // Optional tenantId query parameter: if provided, deletes from that tenant's database; otherwise deletes from global database
        adminGroup.MapDelete("/files/{id:int}", async (
            int id,
            [FromQuery] string? tenantId,
            ITenantConfigurationProvider tenantConfigProvider,
            ILocalizationService localizationService,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await CreateScopeWithTenantAsync(
                serviceProvider, tenantId, tenantConfigProvider, cancellationToken);
            
            using var scope = scopeResult.scope;
            var tenantContext = scopeResult.tenantContext;

            if (!string.IsNullOrWhiteSpace(tenantId) && tenantContext?.CurrentTenant == null)
            {
                return Results.NotFound(new 
                { 
                    error = localizationService.GetString(LocalizationKeys.Exceptions.TenantNotFound),
                    message = $"Tenant '{tenantId}' not found" 
                });
            }

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var command = new DeleteFileCommand(id);
            var result = await mediator.Send(command, cancellationToken);
            return result ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
        .WithName("DeleteFileAdmin")
        .WithMetadata(new BypassTenantAttribute())
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        // Delete all temp files - Admin only (cross-tenant)
        adminGroup.MapDelete("/files/temp/all", async (
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteAllTempFilesCommand();
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(new { DeletedCount = result });
        })
        .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
        .WithName("DeleteAllTempFiles")
        .WithMetadata(new BypassTenantAttribute())
        .Produces<object>();

        // Delete old temp files - Admin only (cross-tenant)
        adminGroup.MapDelete("/files/temp/old", async (
            IMediator mediator,
            CancellationToken cancellationToken,
            [FromQuery] int olderThanDays = 7) =>
        {
            var command = new DeleteOldTempFilesCommand(olderThanDays);
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(new { DeletedCount = result, OlderThanDays = olderThanDays });
        })
        .RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"))
        .WithName("DeleteOldTempFiles")
        .WithMetadata(new BypassTenantAttribute())
        .Produces<object>();

        // ============================================
        // INTERNAL SERVICE ENDPOINTS (service-to-service only, bypasses rate limiting)
        // ============================================
        
        var internalGroup = app.MapGroup("/api/filemanager/internal")
            .WithTags("FileManager - Internal")
            .DisableRateLimiting() // Skip rate limiting for service-to-service communication
            .WithMetadata(new BypassTenantAttribute()); // Skip tenant middleware

        // Get file by ID - Internal service-to-service endpoint
        // Ultra-fast: bypasses rate limiting, tenant middleware, and authorization
        // Only validates X-Service-Secret header (done by ServiceAuthenticationMiddleware)
        internalGroup.MapGet("/files/{id:int}", async (
            int id,
            [FromQuery] string? tenantId,
            ITenantConfigurationProvider tenantConfigProvider,
            HttpContext httpContext,
            ILogger<Program> logger,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
        {
            // Validate this is a service-to-service call
            var isService = httpContext.User.HasClaim("IsInternalService", "true");
            if (!isService)
            {
                logger.LogWarning("Internal endpoint access denied - missing IsInternalService claim");
                return Results.Json(null, statusCode: StatusCodes.Status403Forbidden);
            }

            // Create scope with tenant context set before resolving dependencies
            var scopeResult = await CreateScopeWithTenantAsync(
                serviceProvider, tenantId, tenantConfigProvider, cancellationToken);
            
            using var scope = scopeResult.scope;
            
            // Resolve MediatR from the new scope - DbContext will be fresh and see the tenant context
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            
            var query = new GetFileByIdQuery(id);
            var result = await mediator.Send(query, cancellationToken);
            
            // Return null instead of 404 for graceful error handling
            return Results.Ok(result);
        })
        .WithName("GetFileByIdInternal")
        .AllowAnonymous() // No JWT required - ServiceAuthenticationMiddleware handles auth via X-Service-Secret
        .Produces<FileManagerResponse?>()
        .ExcludeFromDescription(); // Hide from Swagger/public documentation

        // Get multiple files by IDs - Batch endpoint for efficient bulk retrieval
        internalGroup.MapGet("/files/batch", async (
            HttpContext httpContext,
            [FromQuery] string? tenantId,
            ITenantConfigurationProvider tenantConfigProvider,
            ILogger<Program> logger,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
        {
            // Validate service-to-service call
            var isService = httpContext.User.HasClaim("IsInternalService", "true");
            if (!isService)
            {
                logger.LogWarning("Internal batch endpoint access denied - missing claim");
                return Results.Json(new List<FileManagerResponse>(), statusCode: StatusCodes.Status403Forbidden);
            }

            // Parse fileIds from query string (supports multiple ?fileIds=1&fileIds=2&fileIds=3)
            var fileIdsStrings = httpContext.Request.Query["fileIds"].ToList();
            var fileIds = fileIdsStrings
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .ToList();

            if (!fileIds.Any())
            {
                return Results.Ok(new List<FileManagerResponse>());
            }

            // Create scope with tenant context set BEFORE DbContext resolution
            var scopeResult = await CreateScopeWithTenantAsync(
                serviceProvider, tenantId, tenantConfigProvider, cancellationToken);
            
            using var scope = scopeResult.scope;
            
            // Resolve MediatR from the new scope
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            
            var query = new GetFilesByIdsQuery(fileIds);
            var result = await mediator.Send(query, cancellationToken);
            
            return Results.Ok(result);
        })
        .WithName("GetFilesByIdsInternal")
        .AllowAnonymous()
        .Produces<List<FileManagerResponse>>()
        .ExcludeFromDescription();

        // Download file by ID
        group.MapGet("/files/{id:int}/download", async (
            int id,
            IMediator mediator,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILocalizationService localizationService,
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
                return Results.NotFound(new { error = localizationService.GetString(LocalizationKeys.Exceptions.FileNotFoundOnDisk), path = physicalPath });

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
