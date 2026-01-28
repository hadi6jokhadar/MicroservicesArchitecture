using MediatR;
using Microsoft.AspNetCore.Mvc;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Application.Queries;

namespace Translation.API.Endpoints;

public static class TranslationEndpoints
{
    public static void MapTranslationEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup("/api/translations")
            .WithTags("Translations - Public");

        var adminGroup = app.MapGroup("/api/translations")
            .WithTags("Translations - Admin")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        // ============================================
        // Public Endpoints (Anyone can access)
        // ============================================
        
        /// <summary>
        /// Get translations for a specific language
        /// Supports optional tenant-specific overrides via x-tenant-id header
        /// If tenantId is NOT provided: returns only global translations (TenantId = null)
        /// If tenantId IS provided: returns global translations + tenant-specific overrides
        /// Supports optional category filtering via query parameter
        /// </summary>
        publicGroup.MapGet("/{language}", async (
            [FromRoute] string language,
            [FromQuery] string? category,
            [FromHeader(Name = "x-tenant-id")] string? tenantId,
            IMediator mediator) =>
        {
            var query = new GetTranslationsQuery(language, tenantId, category);
            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("GetTranslations")
        .WithDescription("Get all translations for a language. If tenantId header is absent, returns only global translations. If present, returns global + tenant-specific overrides.")
        .Produces<TranslationsDto>();
        
        // ============================================
        // Admin Endpoints (Require Admin/SuperAdmin Role)
        // ============================================
        
        /// <summary>
        /// Get paginated list of translation keys
        /// </summary>
        adminGroup.MapGet("/keys", async (
            [AsParameters] GetTranslationKeysQuery query,
            IMediator mediator) =>
        {
            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetTranslationKeys")
        .WithDescription("Get paginated list of translation keys with optional filtering by category and search term (admin only)")
        .Produces<PaginatedList<TranslationKeyDto>>();
        
        /// <summary>
        /// Create a new translation key
        /// </summary>
        adminGroup.MapPost("/keys", async (
            [FromBody] CreateTranslationKeyCommand command,
            IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/api/translations/keys/{result.Id}", result);
        })
        .WithName("CreateTranslationKey")
        .WithDescription("Create a new translation key (admin only)")
        .Produces<TranslationKeyDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();
        
        /// <summary>
        /// Update a translation key
        /// </summary>
        adminGroup.MapPut("/keys/{id:int}", async (
            int id,
            [FromBody] UpdateTranslationKeyCommand command,
            IMediator mediator) =>
        {
            if (id != command.Id)
            {
                return Results.BadRequest(new { error = "ID mismatch" });
            }
            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .WithName("UpdateTranslationKey")
        .WithDescription("Update a translation key's description (admin only)")
        .Produces<TranslationKeyDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();
        
        /// <summary>
        /// Delete a translation key
        /// </summary>
        adminGroup.MapDelete("/keys/{id:int}", async (
            int id,
            IMediator mediator) =>
        {
            var command = new DeleteTranslationKeyCommand(id);
            var result = await mediator.Send(command);
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteTranslationKey")
        .WithDescription("Delete a translation key and all its values (admin only)")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
        
        /// <summary>
        /// Set or update a translation value
        /// If TenantId is null in command, it's a global translation
        /// If TenantId is provided, it's a tenant-specific override
        /// </summary>
        adminGroup.MapPost("/values", async (
            [FromBody] SetTranslationCommand command,
            IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .WithName("SetTranslation")
        .WithDescription("Set or update a translation value (global or tenant-specific) (admin only)")
        .Produces<TranslationValueDto>()
        .ProducesValidationProblem();
        
        /// <summary>
        /// Delete a specific translation value
        /// </summary>
        adminGroup.MapDelete("/values/{id:int}", async (
            int id,
            IMediator mediator) =>
        {
            var command = new DeleteTranslationValueCommand(id);
            var result = await mediator.Send(command);
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteTranslationValue")
        .WithDescription("Delete a specific translation value (admin only)")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
        
        /// <summary>
        /// Bulk import translations from JSON
        /// </summary>
        adminGroup.MapPost("/import", async (
            [FromBody] ImportTranslationsCommand command,
            IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .WithName("ImportTranslations")
        .WithDescription("Bulk import translations from JSON format (admin only)")
        .Produces<ImportTranslationsResult>()
        .ProducesValidationProblem();
    }
}
