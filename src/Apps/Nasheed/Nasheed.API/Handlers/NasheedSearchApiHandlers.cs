using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;

namespace Nasheed.API.Handlers;

public static class NasheedSearchApiHandlers
{
    public static async Task<IResult> Search(
        [AsParameters] SearchSongsQuery query,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
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
