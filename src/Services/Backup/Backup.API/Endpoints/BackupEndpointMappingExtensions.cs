using Asp.Versioning;
using Backup.API.Filters;
using Backup.API.Handlers;
using Backup.Application.Commands;

namespace Backup.API.Endpoints;

public static class BackupEndpointMappingExtensions
{
    /// <summary>Maps all Backup admin API endpoints. Every route requires the SuperAdmin role.</summary>
    public static WebApplication MapBackupEndpoints(this WebApplication app)
    {
        var v1 = app.NewVersionedApi("Backup");
        var adminGroup = v1.MapGroup("/api/v{version:apiVersion}/admin")
            .HasApiVersion(1)
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"))
            .WithTags("Backup Management (Super Admin)");

        adminGroup.MapGet("/backup-targets", BackupApiHandlers.GetBackupTargetsHandler)
            .WithName("GetBackupTargets")
            .WithSummary("Get all configured backup targets")
            .Produces<object>(200);

        adminGroup.MapPatch("/backup-targets/{id:int}", BackupApiHandlers.UpdateBackupTargetHandler)
            .WithName("UpdateBackupTarget")
            .WithSummary("Enable/disable a backup target or change its retention override")
            .Produces<object>(200)
            .Produces(400)
            .Produces(404);

        adminGroup.MapPost("/backups/trigger", BackupApiHandlers.TriggerBackupHandler)
            .WithName("TriggerBackup")
            .WithSummary("Manually trigger a backup run for a service's global DB or a tenant's DB")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<TriggerBackupCommand>>();

        adminGroup.MapGet("/backups/summary", BackupApiHandlers.GetBackupSummaryHandler)
            .WithName("GetBackupSummary")
            .WithSummary("Get one row per backup target, enriched with its most recent run")
            .Produces<object>(200);

        adminGroup.MapGet("/backups", BackupApiHandlers.GetBackupRunsHandler)
            .WithName("GetBackupRuns")
            .WithSummary("Get a paginated, filterable list of backup runs")
            .Produces<object>(200);

        adminGroup.MapGet("/backups/{id:int}", BackupApiHandlers.GetBackupRunByIdHandler)
            .WithName("GetBackupRunById")
            .WithSummary("Get a single backup run by id")
            .Produces<object>(200)
            .Produces(404);

        adminGroup.MapPost("/backups/{id:int}/restore", BackupApiHandlers.TriggerRestoreHandler)
            .WithName("TriggerRestore")
            .WithSummary("Trigger a restore from a completed backup run — requires Confirm=true")
            .Produces<object>(200)
            .Produces(400)
            .Produces(404)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<TriggerRestoreCommand>>();

        adminGroup.MapGet("/restores", BackupApiHandlers.GetRestoreRunsHandler)
            .WithName("GetRestoreRuns")
            .WithSummary("Get a paginated list of restore runs")
            .Produces<object>(200);

        return app;
    }
}
