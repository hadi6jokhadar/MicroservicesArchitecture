using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public interface IRoleClaimRepository
{
    Task AssignClaimsToRoleAsync(int roleId, List<int> claimIds, CancellationToken cancellationToken = default);
    Task RevokeClaimsFromRoleAsync(int roleId, List<int> claimIds, CancellationToken cancellationToken = default);
    Task RevokeAllClaimsFromRoleAsync(int roleId, CancellationToken cancellationToken = default);
    Task<bool> RoleHasClaimAsync(int roleId, int claimId, CancellationToken cancellationToken = default);
    Task<List<RoleClaim>> GetRoleClaimAssignmentsAsync(int roleId, CancellationToken cancellationToken = default);
}
