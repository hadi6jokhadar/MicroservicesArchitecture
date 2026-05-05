using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Commands;

public record AddFavoriteCommand(int SongId, int UserId) : IRequest<FavoriteDto>;
public record RemoveFavoriteCommand(int SongId, int UserId) : IRequest<bool>;
public record AddRatingCommand(int SongId, int UserId, int Value) : IRequest<RatingDto>;
public record AddPlayLogCommand(int SongId, int UserId) : IRequest<bool>;
