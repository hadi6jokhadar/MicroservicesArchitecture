using IhsanDev.Shared.Infrastructure.Persistence;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;

namespace Nasheed.Domain.Interfaces;

public interface ISongRepository : IRepository<SongEntity>
{
    Task<(List<SongEntity> Items, int TotalCount)> GetAllAsync(
        string? textFilter = null,
        int? artistId = null,
        SongState? state = null,
        string? copyrightRiskLevel = null,
        string? contentSafetyFlag = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<List<SongEntity>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    Task<List<SongEntity>> GetByArtistIdAsync(int artistId, CancellationToken cancellationToken = default);
}
