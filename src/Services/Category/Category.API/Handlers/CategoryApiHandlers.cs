using MediatR;
using Microsoft.AspNetCore.Mvc;
using Category.Application.Commands;
using Category.Application.DTOs;
using Category.Application.Queries;

namespace Category.API.Handlers;

public static class CategoryApiHandlers
{
    public static async Task<IResult> Create(
        [FromBody] CreateCategoryCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Results.Created($"/api/categories/{result.Id}", result);
    }

    public static async Task<IResult> GetById(
        int id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetCategoryByIdQuery(id), ct);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }

    public static async Task<IResult> GetAll(
        [AsParameters] GetCategoryListQuery query,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetTree(
        [FromQuery] string? textFilter,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetCategoryTreeQuery(textFilter), ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Update(
        int id,
        [FromBody] UpdateCategoryCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command with { Id = id }, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Move(
        int id,
        [FromBody] MoveCategoryCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command with { Id = id }, ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> Delete(
        int id,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new DeleteCategoryCommand(id), ct);
        return Results.NoContent();
    }
}
