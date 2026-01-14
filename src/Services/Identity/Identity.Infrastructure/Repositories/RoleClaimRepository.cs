using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class RoleClaimRepository : IRoleClaimRepository
{
    private readonly IdentityDbContext _context;

    public RoleClaimRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task AssignClaimsToRoleAsync(int roleId, List<int> claimIds, CancellationToken cancellationToken = default)
    {
        // Get existing claim assignments
        var existingClaimIds = await _context.RoleClaims
            .Where(rc => rc.RoleId == roleId)
            .Select(rc => rc.ClaimId)
            .ToListAsync(cancellationToken);

        // Find new claims to add
        var newClaimIds = claimIds.Except(existingClaimIds).ToList();

        // Add new claim assignments
        foreach (var claimId in newClaimIds)
        {
            _context.RoleClaims.Add(new RoleClaim
            {
                RoleId = roleId,
                ClaimId = claimId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeClaimsFromRoleAsync(int roleId, List<int> claimIds, CancellationToken cancellationToken = default)
    {
        var roleClaims = await _context.RoleClaims
            .Where(rc => rc.RoleId == roleId && claimIds.Contains(rc.ClaimId))
            .ToListAsync(cancellationToken);

        _context.RoleClaims.RemoveRange(roleClaims);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllClaimsFromRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var roleClaims = await _context.RoleClaims
            .Where(rc => rc.RoleId == roleId)
            .ToListAsync(cancellationToken);

        _context.RoleClaims.RemoveRange(roleClaims);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RoleHasClaimAsync(int roleId, int claimId, CancellationToken cancellationToken = default)
    {
        return await _context.RoleClaims
            .AnyAsync(rc => rc.RoleId == roleId && rc.ClaimId == claimId, cancellationToken);
    }

    public async Task<List<RoleClaim>> GetRoleClaimAssignmentsAsync(int roleId, CancellationToken cancellationToken = default)
    {
        return await _context.RoleClaims
            .Include(rc => rc.Claim)
            .Where(rc => rc.RoleId == roleId)
            .ToListAsync(cancellationToken);
    }
}
