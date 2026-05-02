using Microsoft.EntityFrameworkCore;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class FavoriteRepository : IFavoriteRepository
{
    private readonly NasheedDbContext _context;

    public FavoriteRepository(NasheedDbContext context) => _context = context;

    public async Task<FavoriteEntity?> GetAsync(string userId, int songId, CancellationToken cancellationToken = default)
    {
        return await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.SongId == songId, cancellationToken);
    }

    public async Task<List<FavoriteEntity>> GetUserFavoritesAsync(string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Favorites
            .Include(f => f.Song)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountUserFavoritesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Favorites.CountAsync(f => f.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(FavoriteEntity favorite, CancellationToken cancellationToken = default)
    {
        await _context.Favorites.AddAsync(favorite, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(FavoriteEntity favorite, CancellationToken cancellationToken = default)
    {
        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string userId, int songId, CancellationToken cancellationToken = default)
    {
        return await _context.Favorites.AnyAsync(f => f.UserId == userId && f.SongId == songId, cancellationToken);
    }
}
