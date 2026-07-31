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
        // SuperAdmin-only, cross-tenant view (see Category.API's CategoryEndpoints.cs for the
        // same pattern): "Admin" is an ordinary per-tenant role, and per this repo's convention
        // it must never be mixed into a [BypassTenant] group, since that would let a
        // tenant-scoped Admin reach every tenant's audit rows, not just their own. The Angular
        // admin app's route guard already restricts /audit-log to SuperAdmin
        // (pages.routes.ts), and AuditLogService intentionally never sends an x-tenant-id
        // header — so this group must actually be [BypassTenant] to match, instead of falling
        // through to TenantMiddleware's "x-tenant-id header is required" 400. That 400 has no
        // CORS headers (TenantMiddleware runs before UseTenantAwareCors in every service's
        // pipeline), which the browser then misreports as a CORS failure rather than the real
        // 400 — fixed here rather than by re-adding OptionalTenant/CORS workarounds.
        var group = app.MapGroup("/api/admin/audit-logs")
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"))
            .WithMetadata(new BypassTenantAttribute())
            .WithTags("Audit Logs");

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
