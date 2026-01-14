using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public interface IClaimRepository
{
    Task<Claim?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Claim?> GetByClaimValueAsync(string claimValue, CancellationToken cancellationToken = default);
    Task<List<Claim>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<List<Claim>> GetUserClaimsAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<Claim>> GetRoleClaimsAsync(int roleId, CancellationToken cancellationToken = default);
    Task<Claim> CreateAsync(Claim claim, CancellationToken cancellationToken = default);
    Task UpdateAsync(Claim claim, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> IsSuperAdminOnlyClaimAsync(int claimId, CancellationToken cancellationToken = default);
}
