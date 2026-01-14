using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public interface IUserRoleRepository
{
    Task AssignRolesToUserAsync(int userId, List<int> roleIds, CancellationToken cancellationToken = default);
    Task RevokeRolesFromUserAsync(int userId, List<int> roleIds, CancellationToken cancellationToken = default);
    Task RevokeAllRolesFromUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> UserHasRoleAsync(int userId, string roleName, CancellationToken cancellationToken = default);
    Task<List<UserRole>> GetUserRoleAssignmentsAsync(int userId, CancellationToken cancellationToken = default);
}
