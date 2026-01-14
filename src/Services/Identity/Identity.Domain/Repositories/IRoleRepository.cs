using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<Role>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<List<Role>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
    Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default);
    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> IsSystemRoleAsync(int roleId, CancellationToken cancellationToken = default);
}
