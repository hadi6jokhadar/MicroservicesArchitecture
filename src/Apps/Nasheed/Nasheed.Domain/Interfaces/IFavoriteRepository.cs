using IhsanDev.Shared.Infrastructure.Persistence;
using Nasheed.Domain.Entities;

namespace Nasheed.Domain.Interfaces;

public interface IFavoriteRepository
{
    Task<FavoriteEntity?> GetAsync(int userId, int songId, CancellationToken cancellationToken = default);
    Task<List<FavoriteEntity>> GetUserFavoritesAsync(int userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountUserFavoritesAsync(int userId, CancellationToken cancellationToken = default);
    Task AddAsync(FavoriteEntity favorite, CancellationToken cancellationToken = default);
    Task RemoveAsync(FavoriteEntity favorite, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int userId, int songId, CancellationToken cancellationToken = default);
    Task DeleteBySongIdAsync(int songId, CancellationToken cancellationToken = default);
}
