using IhsanDev.Shared.Infrastructure.Attributes;
using MediatR;
using Category.API.Filters;
using Category.Application.Events;
using Category.Application.Queries;

namespace Category.API.Endpoints;

/// <summary>
/// Internal service-to-service endpoints. Protected by <see cref="InternalServiceKeyFilter"/>.
/// Not exposed in Swagger and not accessible from the public internet.
///
/// These endpoints are intentionally separate from the public API group.
/// </summary>
public static class CategoryInternalEndpoints
{
    public static IEndpointRouteBuilder MapCategoryInternalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/categories")
            .WithTags("Category - Internal")
            .WithMetadata(new OptionalTenantAttribute())
            .ExcludeFromDescription()           // hide from public Swagger
            .AddEndpointFilter<InternalServiceKeyFilter>();

        // GET /internal/categories/snapshot
        // Returns ALL categories as CategoryEventMessage records so consumer services
        // can seed their local snapshot table on startup before subscribing to Pub/Sub.
        // Pass x-tenant-id header to scope the result to a specific tenant.
        group.MapGet("/snapshot", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCategorySnapshotQuery(), ct);
            return Results.Ok(result);
        })
        .WithName("Internal_GetCategorySnapshot")
        .Produces<List<CategoryEventMessage>>()
        .AllowAnonymous(); // auth is handled by InternalServiceKeyFilter

        return app;
    }
}
