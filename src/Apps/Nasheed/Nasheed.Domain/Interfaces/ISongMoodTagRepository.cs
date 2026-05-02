using IhsanDev.Shared.Infrastructure.Persistence;
using Nasheed.Domain.Entities;

namespace Nasheed.Domain.Interfaces;

public interface ISongMoodTagRepository : IRepository<SongMoodTagEntity>
{
    Task<List<SongMoodTagEntity>> GetBySongIdAsync(int songId, CancellationToken cancellationToken = default);
    Task DeleteBySongIdAsync(int songId, CancellationToken cancellationToken = default);
}
