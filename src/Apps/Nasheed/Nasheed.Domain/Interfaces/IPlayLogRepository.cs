using IhsanDev.Shared.Infrastructure.Persistence;
using Nasheed.Domain.Entities;

namespace Nasheed.Domain.Interfaces;

public interface IPlayLogRepository
{
    Task AddAsync(PlayLogEntity playLog, CancellationToken cancellationToken = default);
    Task DeleteBySongIdAsync(int songId, CancellationToken cancellationToken = default);
}
