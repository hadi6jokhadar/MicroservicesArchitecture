using Backup.Application.Commands;
using Backup.Application.Queries;
using IhsanDev.Shared.Application.Localization;
using MediatR;

namespace Backup.API.Handlers;

public static class BackupApiHandlers
{
    public static async Task<IResult> GetBackupTargetsHandler(
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetBackupTargetsQuery(), ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> UpdateBackupTargetHandler(
        int id,
        UpdateBackupTargetCommand command,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        if (id != command.Id)
        {
            return Results.BadRequest(new { message = localizationService.GetString(LocalizationKeys.Exceptions.BadRequest) });
        }

        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> TriggerBackupHandler(
        TriggerBackupCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetBackupRunsHandler(
        IMediator mediator,
        string? scope = null,
        string? serviceName = null,
        string? tenantId = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetBackupRunsQuery(scope, serviceName, tenantId, status, fromDate, toDate, pageNumber, pageSize);
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetBackupSummaryHandler(
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetBackupSummaryQuery(), ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetBackupRunByIdHandler(
        int id,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetBackupRunByIdQuery(id), ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> TriggerRestoreHandler(
        int id,
        TriggerRestoreCommand command,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        if (id != command.BackupRunId)
        {
            return Results.BadRequest(new { message = localizationService.GetString(LocalizationKeys.Exceptions.BadRequest) });
        }

        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetRestoreRunsHandler(
        IMediator mediator,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRestoreRunsQuery(pageNumber, pageSize), ct);
        return Results.Ok(result);
    }
}
