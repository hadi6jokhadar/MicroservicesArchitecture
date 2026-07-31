using Asp.Versioning;
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
        var v1 = app.NewVersionedApi("Categories");
        var group = v1.MapGroup("/api/v{version:apiVersion}/categories")
            .HasApiVersion(1)
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
        var v1Admin = app.NewVersionedApi("CategoriesAdmin");
        // "Admin" is a per-tenant role (see JwtTenantVerificationMiddleware / Identity's seeded
        // roles) — it must never satisfy a [BypassTenant] group, since that would let a
        // tenant-scoped Admin reach every tenant's global/cross-tenant data. Only SuperAdmin
        // (a true platform-level role, carrying no tenant_id claim) may pass here. Matches
        // Tenant.API's own admin group in EndpointMappingExtensions.cs.
        var adminGroup = v1Admin.MapGroup("/api/v{version:apiVersion}/admin/categories")
            .HasApiVersion(1)
            .WithTags("Category - Admin")
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));

        adminGroup.MapGet("/tree", CategoryApiHandlers.GetTree)
            .WithMetadata(new BypassTenantAttribute())
            .WithName("Admin_GetCategoryTree");

        adminGroup.MapGet("/", CategoryApiHandlers.GetAll)
            .WithMetadata(new BypassTenantAttribute())
            .WithName("Admin_GetCategoryList");

        return app;
    }
}
