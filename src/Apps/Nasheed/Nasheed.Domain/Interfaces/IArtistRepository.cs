using IhsanDev.Shared.Infrastructure.Persistence;
using Nasheed.Domain.Entities;

namespace Nasheed.Domain.Interfaces;

public interface IArtistRepository : IRepository<ArtistEntity>
{
    Task<(List<ArtistEntity> Items, int TotalCount)> GetAllAsync(
        string? textFilter = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
}
