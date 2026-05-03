using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;

namespace Nasheed.API.Handlers;

public static class NasheedSearchApiHandlers
{
    public static async Task<IResult> Search(
        [FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "query")] string? query,
        [FromQuery] int topN,
        IMediator mediator,
        CancellationToken ct)
    {
        var normalizedQuery = !string.IsNullOrWhiteSpace(q) ? q : query ?? string.Empty;
        var result = await mediator.Send(new SearchSongsQuery(normalizedQuery, topN), ct);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetSimilar(
        int songId,
        int topN,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetSimilarSongsQuery(songId, topN), ct);
        return Results.Ok(result);
    }
}
