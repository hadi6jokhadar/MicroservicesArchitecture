using Microsoft.EntityFrameworkCore;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class RatingRepository : IRatingRepository
{
    private readonly NasheedDbContext _context;

    public RatingRepository(NasheedDbContext context) => _context = context;

    public async Task<RatingEntity?> GetAsync(string userId, int songId, CancellationToken cancellationToken = default)
    {
        return await _context.Ratings
            .FirstOrDefaultAsync(r => r.UserId == userId && r.SongId == songId, cancellationToken);
    }

    public async Task AddAsync(RatingEntity rating, CancellationToken cancellationToken = default)
    {
        await _context.Ratings.AddAsync(rating, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RatingEntity rating, CancellationToken cancellationToken = default)
    {
        _context.Ratings.Update(rating);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBySongIdAsync(int songId, CancellationToken cancellationToken = default)
    {
        var ratings = await _context.Ratings
            .Where(r => r.SongId == songId)
            .ToListAsync(cancellationToken);

        if (ratings.Count > 0)
        {
            _context.Ratings.RemoveRange(ratings);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
