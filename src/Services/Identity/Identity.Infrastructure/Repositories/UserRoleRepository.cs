using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class UserRoleRepository : IUserRoleRepository
{
    private readonly IdentityDbContext _context;

    public UserRoleRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task AssignRolesToUserAsync(int userId, List<int> roleIds, CancellationToken cancellationToken = default)
    {
        // Get existing role assignments
        var existingRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        // Find new roles to add
        var newRoleIds = roleIds.Except(existingRoleIds).ToList();

        // Add new role assignments
        foreach (var roleId in newRoleIds)
        {
            _context.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeRolesFromUserAsync(int userId, List<int> roleIds, CancellationToken cancellationToken = default)
    {
        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == userId && roleIds.Contains(ur.RoleId))
            .ToListAsync(cancellationToken);

        _context.UserRoles.RemoveRange(userRoles);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllRolesFromUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);

        _context.UserRoles.RemoveRange(userRoles);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UserHasRoleAsync(int userId, string roleName, CancellationToken cancellationToken = default)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();
        return await _context.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur => ur.UserId == userId && ur.Role.NormalizedName == normalizedRoleName, cancellationToken);
    }

    public async Task<List<UserRole>> GetUserRoleAssignmentsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);
    }
}
