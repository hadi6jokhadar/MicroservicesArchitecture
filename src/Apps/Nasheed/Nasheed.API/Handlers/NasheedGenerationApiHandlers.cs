using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;

namespace Nasheed.API.Handlers;

public static class NasheedGenerationApiHandlers
{
    public static async Task<IResult> GenerateLyrics(
        [FromBody] GenerateLyricsCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }
}
