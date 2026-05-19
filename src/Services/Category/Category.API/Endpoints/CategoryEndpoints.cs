using IhsanDev.Shared.Infrastructure.Attributes;
using IhsanDev.Shared.Infrastructure.Filters;
using Category.API.Filters;
using Category.API.Handlers;
using Category.Application.Commands;
using Category.Application.DTOs;

namespace Category.API.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        // ── TENANT ENDPOINTS (optional tenant — uses appSettings DB when no x-tenant-id) ──
        var group = app.MapGroup("/api/categories")
            .WithTags("Category Management")
            .RequireAuthorization()
            .WithMetadata(new OptionalTenantAttribute());

        group.MapPost("/", CategoryApiHandlers.Create)
            .WithName("CreateCategory")
            .Produces<CategoryDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .AddEndpointFilter<ValidationFilter<CreateCategoryCommand>>();

        group.MapGet("/tree", CategoryApiHandlers.GetTree)
            .WithName("GetCategoryTree")
            .Produces<List<CategoryDto>>();

        group.MapGet("/{id:int}", CategoryApiHandlers.GetById)
            .WithName("GetCategoryById")
            .Produces<CategoryDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", CategoryApiHandlers.GetAll)
            .WithName("GetCategoryList")
            .Produces<PaginatedList<CategoryDto>>();

        group.MapPut("/{id:int}", CategoryApiHandlers.Update)
            .WithName("UpdateCategory")
            .Produces<CategoryDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .AddEndpointFilter<ValidationFilter<UpdateCategoryCommand>>();

        group.MapPut("/{id:int}/move", CategoryApiHandlers.Move)
            .WithName("MoveCategory")
            .Produces<CategoryDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .AddEndpointFilter<ValidationFilter<MoveCategoryCommand>>();

        group.MapDelete("/{id:int}", CategoryApiHandlers.Delete)
            .WithName("DeleteCategory")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // ── ADMIN ENDPOINTS (bypass tenant) ──────────────────────────────────
        var adminGroup = app.MapGroup("/api/admin/categories")
            .WithTags("Category - Admin")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        adminGroup.MapGet("/tree", CategoryApiHandlers.GetTree)
            .WithMetadata(new BypassTenantAttribute())
            .WithName("Admin_GetCategoryTree");

        adminGroup.MapGet("/", CategoryApiHandlers.GetAll)
            .WithMetadata(new BypassTenantAttribute())
            .WithName("Admin_GetCategoryList");

        return app;
    }
}
