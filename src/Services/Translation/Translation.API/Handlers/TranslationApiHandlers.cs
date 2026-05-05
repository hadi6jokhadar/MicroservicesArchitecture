using MediatR;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Application.Queries;

namespace Translation.API.Handlers;

public static class TranslationApiHandlers
{
    public static async Task<IResult> GetTranslationsHandler(
        string language,
        string? category,
        string? tenantId,
        HttpContext httpContext,
        IMediator mediator)
    {
        // Allow tenantId from x-tenant-id header as well as from query string
        tenantId = tenantId
            ?? httpContext.Request.Headers["x-tenant-id"].FirstOrDefault();

        var query = new GetTranslationsQuery(language, tenantId, category);
        var result = await mediator.Send(query);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetTranslationKeysHandler(
        [AsParameters] GetTranslationKeysQuery query,
        HttpContext httpContext,
        IMediator mediator)
    {
        // Allow tenantId from x-tenant-id header as well as from query string
        var tenantId = query.TenantId
            ?? httpContext.Request.Headers["x-tenant-id"].FirstOrDefault();

        var resolvedQuery = query with { TenantId = tenantId };
        var result = await mediator.Send(resolvedQuery);
        return Results.Ok(result);
    }

    public static async Task<IResult> CreateTranslationKeyHandler(
        CreateTranslationKeyCommand command,
        IMediator mediator)
    {
        var result = await mediator.Send(command);
        return Results.Created($"/api/translations/keys/{result.Id}", result);
    }

    public static async Task<IResult> UpdateTranslationKeyHandler(
        int id,
        UpdateTranslationKeyCommand command,
        IMediator mediator)
    {
        if (id != command.Id)
        {
            return Results.BadRequest(new { error = "ID mismatch" });
        }
        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    public static async Task<IResult> DeleteTranslationKeyHandler(
        int id,
        IMediator mediator)
    {
        var command = new DeleteTranslationKeyCommand(id);
        var result = await mediator.Send(command);
        return result ? Results.NoContent() : Results.NotFound();
    }

    public static async Task<IResult> ToggleTranslationKeyArchivedStatusHandler(
        int id,
        IMediator mediator)
    {
        var command = new ToggleTranslationKeyArchivedStatusCommand(id);
        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    public static async Task<IResult> SetTranslationHandler(
        SetTranslationCommand command,
        IMediator mediator)
    {
        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    public static async Task<IResult> DeleteTranslationValueHandler(
        int id,
        IMediator mediator)
    {
        var command = new DeleteTranslationValueCommand(id);
        var result = await mediator.Send(command);
        return result ? Results.NoContent() : Results.NotFound();
    }

    public static async Task<IResult> ImportTranslationsHandler(
        ImportTranslationsCommand command,
        IMediator mediator)
    {
        var result = await mediator.Send(command);
        return Results.Ok(result);
    }
}
