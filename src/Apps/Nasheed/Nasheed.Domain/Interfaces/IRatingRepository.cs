using Nasheed.Domain.Entities;

namespace Nasheed.Domain.Interfaces;

public interface IRatingRepository
{
    Task<RatingEntity?> GetAsync(string userId, int songId, CancellationToken cancellationToken = default);
    Task AddAsync(RatingEntity rating, CancellationToken cancellationToken = default);
    Task UpdateAsync(RatingEntity rating, CancellationToken cancellationToken = default);
}
