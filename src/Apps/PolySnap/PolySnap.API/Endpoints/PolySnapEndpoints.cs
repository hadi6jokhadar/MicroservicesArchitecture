using PolySnap.API.Filters;
using PolySnap.API.Handlers;
using PolySnap.Application.Commands;
using PolySnap.Application.DTOs;

namespace PolySnap.API.Endpoints;

public static class PolySnapEndpoints
{
    public static IEndpointRouteBuilder MapPolySnapEndpoints(this IEndpointRouteBuilder app)
    {
        // ── SNAP REQUESTS ────────────────────────────────────
        var v1SnapRequests = app.NewVersionedApi("SnapRequests");
        var snapRequests = v1SnapRequests.MapGroup("/api/v{version:apiVersion}/snap-requests")
            .HasApiVersion(1)
            .WithTags("Snap Requests")
            .RequireAuthorization();

        snapRequests.MapPost("/", SnapRequestApiHandlers.Create)
            .WithName("CreateSnapRequest")
            .Produces<SnapRequestDto>(StatusCodes.Status201Created)
            .AddEndpointFilter<ValidationFilter<CreateSnapRequestCommand>>();

        snapRequests.MapGet("/{id:int}", SnapRequestApiHandlers.GetById)
            .WithName("GetSnapRequestById")
            .Produces<SnapRequestDto>()
            .Produces(StatusCodes.Status404NotFound);

        snapRequests.MapGet("/", SnapRequestApiHandlers.GetAll)
            .WithName("GetSnapRequestList")
            .Produces<PaginatedList<SnapRequestDto>>();

        snapRequests.MapPut("/{id:int}", SnapRequestApiHandlers.Update)
            .WithName("UpdateSnapRequest")
            .Produces<SnapRequestDto>()
            .AddEndpointFilter<ValidationFilter<UpdateSnapRequestCommand>>();

        snapRequests.MapDelete("/{id:int}", SnapRequestApiHandlers.Delete)
            .WithName("DeleteSnapRequest")
            .Produces(StatusCodes.Status200OK);

        return app;
    }
}
