using MediatR;
using Microsoft.AspNetCore.Mvc;
using Translation.API.Filters;
using Translation.API.Handlers;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Application.Queries;
using IhsanDev.Shared.Application.Common.Models;

namespace Translation.API.Extensions;

public static class EndpointMappingExtensions
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
        publicGroup.MapGet("/{language}", TranslationApiHandlers.GetTranslationsHandler)
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
        adminGroup.MapGet("/keys", TranslationApiHandlers.GetTranslationKeysHandler)
        .WithName("GetTranslationKeys")
        .WithDescription("Get paginated list of translation keys with optional filtering by category and search term (admin only)")
        .Produces<PaginatedList<TranslationKeyDto>>();
        
        /// <summary>
        /// Create a new translation key
        /// </summary>
        adminGroup.MapPost("/keys", TranslationApiHandlers.CreateTranslationKeyHandler)
        .WithName("CreateTranslationKey")
        .WithDescription("Create a new translation key (admin only)")
        .Produces<TranslationKeyDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateTranslationKeyCommand>>();
        
        /// <summary>
        /// Update a translation key
        /// </summary>
        adminGroup.MapPut("/keys/{id:int}", TranslationApiHandlers.UpdateTranslationKeyHandler)
        .WithName("UpdateTranslationKey")
        .WithDescription("Update a translation key's description (admin only)")
        .Produces<TranslationKeyDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<UpdateTranslationKeyCommand>>();
        
        /// <summary>
        /// Delete a translation key
        /// </summary>
        adminGroup.MapDelete("/keys/{id:int}", TranslationApiHandlers.DeleteTranslationKeyHandler)
        .WithName("DeleteTranslationKey")
        .WithDescription("Delete a translation key and all its values (admin only)")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        /// <summary>
        /// Toggle translation key archived status
        /// </summary>
        adminGroup.MapPatch("/keys/{id:int}/toggle-archive", TranslationApiHandlers.ToggleTranslationKeyArchivedStatusHandler)
        .WithName("ToggleTranslationKeyArchivedStatus")
        .WithDescription("Archive or unarchive translation key (admin only)")
        .Produces<TranslationKeyDto>()
        .Produces(StatusCodes.Status404NotFound);
        
        /// <summary>
        /// Set or update a translation value
        /// If TenantId is null in command, it's a global translation
        /// If TenantId is provided, it's a tenant-specific override
        /// </summary>
        adminGroup.MapPost("/values", TranslationApiHandlers.SetTranslationHandler)
        .WithName("SetTranslation")
        .WithDescription("Set or update a translation value (global or tenant-specific) (admin only)")
        .Produces<TranslationValueDto>()
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<SetTranslationCommand>>();
        
        /// <summary>
        /// Delete a specific translation value
        /// </summary>
        adminGroup.MapDelete("/values/{id:int}", TranslationApiHandlers.DeleteTranslationValueHandler)
        .WithName("DeleteTranslationValue")
        .WithDescription("Delete a specific translation value (admin only)")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
        
        /// <summary>
        /// Bulk import translations from JSON
        /// </summary>
        adminGroup.MapPost("/import", TranslationApiHandlers.ImportTranslationsHandler)
        .WithName("ImportTranslations")
        .WithDescription("Bulk import translations from JSON format (admin only)")
        .Produces<ImportTranslationsResult>()
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<ImportTranslationsCommand>>();
    }
}
