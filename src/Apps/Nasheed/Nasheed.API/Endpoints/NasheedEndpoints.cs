using Nasheed.API.Filters;
using Nasheed.API.Handlers;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;

namespace Nasheed.API.Endpoints;

public static class NasheedEndpoints
{
    public static IEndpointRouteBuilder MapNasheedEndpoints(this IEndpointRouteBuilder app)
    {
        // ── ARTISTS ──────────────────────────────────────────
        var artists = app.MapGroup("/api/artists")
            .WithTags("Artists")
            .RequireAuthorization();

        artists.MapPost("/", NasheedArtistApiHandlers.Create)
            .WithName("CreateArtist")
            .Produces<ArtistDto>(StatusCodes.Status201Created)
            .AddEndpointFilter<ValidationFilter<CreateArtistCommand>>();

        artists.MapGet("/{id:int}", NasheedArtistApiHandlers.GetById)
            .WithName("GetArtistById")
            .Produces<ArtistDto>()
            .Produces(StatusCodes.Status404NotFound);

        artists.MapGet("/", NasheedArtistApiHandlers.GetAll)
            .WithName("GetArtistList")
            .Produces<PaginatedList<ArtistDto>>();

        artists.MapPut("/{id:int}", NasheedArtistApiHandlers.Update)
            .WithName("UpdateArtist")
            .Produces<ArtistDto>()
            .AddEndpointFilter<ValidationFilter<UpdateArtistCommand>>();

        artists.MapDelete("/{id:int}", NasheedArtistApiHandlers.Delete)
            .WithName("DeleteArtist")
            .Produces(StatusCodes.Status204NoContent);

        // ── SONGS ─────────────────────────────────────────────
        var songs = app.MapGroup("/api/songs")
            .WithTags("Songs")
            .RequireAuthorization();

        songs.MapPost("/", NasheedSongApiHandlers.Create)
            .WithName("CreateSong")
            .Produces<SongDto>(StatusCodes.Status201Created)
            .AddEndpointFilter<ValidationFilter<CreateSongCommand>>();

        songs.MapGet("/{id:int}", NasheedSongApiHandlers.GetById)
            .WithName("GetSongById")
            .Produces<SongDto>()
            .Produces(StatusCodes.Status404NotFound);

        songs.MapGet("/", NasheedSongApiHandlers.GetAll)
            .WithName("GetSongList")
            .Produces<PaginatedList<SongDto>>();

        songs.MapPut("/{id:int}", NasheedSongApiHandlers.Update)
            .WithName("UpdateSong")
            .Produces<SongDto>()
            .AddEndpointFilter<ValidationFilter<UpdateSongCommand>>();

        songs.MapDelete("/{id:int}", NasheedSongApiHandlers.Delete)
            .WithName("DeleteSong")
            .Produces(StatusCodes.Status204NoContent);

        songs.MapGet("/{id:int}/analysis", NasheedSongApiHandlers.GetAnalysisStatus)
            .WithName("GetSongAnalysisStatus")
            .Produces<SongDto>()
            .Produces(StatusCodes.Status404NotFound);

        songs.MapGet("/{id:int}/similar", (int id, int topN, MediatR.IMediator mediator, CancellationToken ct)
                => NasheedSearchApiHandlers.GetSimilar(id, topN <= 0 ? 10 : topN, mediator, ct))
            .WithName("GetSimilarSongs")
            .Produces<List<SearchResultDto>>();

        // ── INGESTION ─────────────────────────────────────────
        var ingestion = app.MapGroup("/api/ingestion")
            .WithTags("Ingestion")
            .RequireAuthorization();

        ingestion.MapGet("/{id:int}", NasheedIngestionApiHandlers.GetById)
            .WithName("GetIngestionJobById")
            .Produces<IngestionJobDto>()
            .Produces(StatusCodes.Status404NotFound);

        ingestion.MapGet("/", NasheedIngestionApiHandlers.GetAll)
            .WithName("GetIngestionJobList")
            .Produces<PaginatedList<IngestionJobDto>>();

        ingestion.MapPost("/{id:int}/retry", NasheedIngestionApiHandlers.Retry)
            .WithName("RetryIngestionJob")
            .Produces<IngestionJobDto>();

        ingestion.MapDelete("/{id:int}", NasheedIngestionApiHandlers.Remove)
            .WithName("RemoveIngestionJob")
            .Produces<bool>()
            .Produces(StatusCodes.Status404NotFound);

        ingestion.MapPost("/songs/{songId:int}/reindex", NasheedIngestionApiHandlers.Reindex)
            .WithName("ReindexSong")
            .Produces<IngestionJobDto>();

        // ── SEARCH ────────────────────────────────────────────
        var search = app.MapGroup("/api/search")
            .WithTags("Search")
            .RequireAuthorization();

        search.MapGet("/", NasheedSearchApiHandlers.Search)
            .WithName("SearchSongs")
            .Produces<List<SearchResultDto>>();

        // ── INTERACTIONS ──────────────────────────────────────
        var interactions = app.MapGroup("/api/songs")
            .WithTags("Interactions")
            .RequireAuthorization();

        interactions.MapPost("/{songId:int}/favorites", NasheedInteractionApiHandlers.AddFavorite)
            .WithName("AddFavorite")
            .Produces<FavoriteDto>();

        interactions.MapDelete("/{songId:int}/favorites", NasheedInteractionApiHandlers.RemoveFavorite)
            .WithName("RemoveFavorite")
            .Produces(StatusCodes.Status204NoContent);

        interactions.MapPost("/{songId:int}/ratings", NasheedInteractionApiHandlers.AddRating)
            .WithName("AddRating")
            .Produces<RatingDto>()
            .AddEndpointFilter<ValidationFilter<AddRatingCommand>>();

        interactions.MapPost("/{songId:int}/play", NasheedInteractionApiHandlers.LogPlay)
            .WithName("LogPlay")
            .Produces(StatusCodes.Status204NoContent);

        // ── GENERATION ────────────────────────────────────────
        var generation = app.MapGroup("/api/generation")
            .WithTags("Generation")
            .RequireAuthorization();

        generation.MapPost("/lyrics", NasheedGenerationApiHandlers.GenerateLyrics)
            .WithName("GenerateLyrics")
            .Produces<GenerateLyricsResponseDto>()
            .AddEndpointFilter<ValidationFilter<GenerateLyricsCommand>>();

        return app;
    }
}
