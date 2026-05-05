using Nasheed.Domain.Entities;

namespace Nasheed.Domain.Interfaces;

public interface IRatingRepository
{
    Task<RatingEntity?> GetAsync(int userId, int songId, CancellationToken cancellationToken = default);
    Task AddAsync(RatingEntity rating, CancellationToken cancellationToken = default);
    Task UpdateAsync(RatingEntity rating, CancellationToken cancellationToken = default);
    Task DeleteBySongIdAsync(int songId, CancellationToken cancellationToken = default);
}
