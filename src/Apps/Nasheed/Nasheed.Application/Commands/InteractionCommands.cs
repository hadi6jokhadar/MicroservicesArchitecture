using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Commands;

public record AddFavoriteCommand(int SongId, string UserId) : IRequest<FavoriteDto>;
public record RemoveFavoriteCommand(int SongId, string UserId) : IRequest<bool>;
public record AddRatingCommand(int SongId, string UserId, int Value) : IRequest<RatingDto>;
public record AddPlayLogCommand(int SongId, string UserId) : IRequest<bool>;
