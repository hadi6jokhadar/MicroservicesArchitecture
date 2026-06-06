using IhsanDev.Shared.Application.Audit;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Infrastructure.Attributes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace IhsanDev.Shared.Infrastructure.Extensions;

public static class AuditLogEndpointExtensions
{
    public static WebApplication MapAuditLogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/audit-logs")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"))
            .WithTags("Audit Logs")
            .WithMetadata(new OptionalTenantAttribute());

        group.MapGet("/", async (
            [AsParameters] GetAuditLogsQuery query,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .WithSummary("Get paginated audit logs with optional filtering and sorting")
        .Produces<PaginatedList<AuditLogDto>>(200);

        return app;
    }
}
