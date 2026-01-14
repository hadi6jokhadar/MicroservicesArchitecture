using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly IdentityDbContext _context;

    public RoleRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Include(r => r.RoleClaims)
                .ThenInclude(rc => rc.Claim)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.ToUpperInvariant();
        return await _context.Roles
            .Include(r => r.RoleClaims)
                .ThenInclude(rc => rc.Claim)
            .FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, cancellationToken);
    }

    public async Task<List<Role>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Roles
            .Include(r => r.RoleClaims)
                .ThenInclude(rc => rc.Claim)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(r => r.Status);
        }

        return await query.OrderBy(r => r.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Role>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
                .ThenInclude(r => r.RoleClaims)
                    .ThenInclude(rc => rc.Claim)
            .Select(ur => ur.Role)
            .Where(r => r.Status)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.ToUpperInvariant();
        return await _context.Roles
            .AnyAsync(r => r.NormalizedName == normalizedName, cancellationToken);
    }

    public async Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
        role.NormalizedName = role.Name.ToUpperInvariant();
        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        role.NormalizedName = role.Name.ToUpperInvariant();
        _context.Roles.Update(role);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles.FindAsync([id], cancellationToken);
        if (role != null)
        {
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> IsSystemRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Where(r => r.Id == roleId)
            .Select(r => r.IsSystemRole)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
