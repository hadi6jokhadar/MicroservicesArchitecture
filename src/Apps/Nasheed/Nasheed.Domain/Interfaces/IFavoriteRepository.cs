using IhsanDev.Shared.Infrastructure.Persistence;
using Nasheed.Domain.Entities;

namespace Nasheed.Domain.Interfaces;

public interface IFavoriteRepository
{
    Task<FavoriteEntity?> GetAsync(string userId, int songId, CancellationToken cancellationToken = default);
    Task<List<FavoriteEntity>> GetUserFavoritesAsync(string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountUserFavoritesAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(FavoriteEntity favorite, CancellationToken cancellationToken = default);
    Task RemoveAsync(FavoriteEntity favorite, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string userId, int songId, CancellationToken cancellationToken = default);
}
