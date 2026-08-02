using IhsanDev.Shared.Infrastructure.Persistence;
using PolySnap.Domain.Entities;

namespace PolySnap.Domain.Interfaces;

public interface ISnapRequestRepository : IRepository<SnapRequestEntity>
{
    Task<(List<SnapRequestEntity> Items, int TotalCount)> GetAllAsync(
        string? textFilter = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
}
