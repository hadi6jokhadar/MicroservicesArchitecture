using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class ClaimRepository : IClaimRepository
{
    private readonly IdentityDbContext _context;

    public ClaimRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Claim?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Claims
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Claim?> GetByClaimValueAsync(string claimValue, CancellationToken cancellationToken = default)
    {
        return await _context.Claims
            .FirstOrDefaultAsync(c => c.ClaimValue == claimValue, cancellationToken);
    }

    public async Task<List<Claim>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Claims.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(c => c.Status);
        }

        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Claim>> GetUserClaimsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RoleClaims)
            .Select(rc => rc.Claim)
            .Where(c => c.Status)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Claim>> GetRoleClaimsAsync(int roleId, CancellationToken cancellationToken = default)
    {
        return await _context.RoleClaims
            .Where(rc => rc.RoleId == roleId)
            .Include(rc => rc.Claim)
            .Select(rc => rc.Claim)
            .Where(c => c.Status)
            .ToListAsync(cancellationToken);
    }

    public async Task<Claim> CreateAsync(Claim claim, CancellationToken cancellationToken = default)
    {
        claim.NormalizedName = claim.Name.ToUpperInvariant();
        _context.Claims.Add(claim);
        await _context.SaveChangesAsync(cancellationToken);
        return claim;
    }

    public async Task UpdateAsync(Claim claim, CancellationToken cancellationToken = default)
    {
        claim.NormalizedName = claim.Name.ToUpperInvariant();
        _context.Claims.Update(claim);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var claim = await _context.Claims.FindAsync([id], cancellationToken);
        if (claim != null)
        {
            _context.Claims.Remove(claim);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> IsSuperAdminOnlyClaimAsync(int claimId, CancellationToken cancellationToken = default)
    {
        return await _context.Claims
            .Where(c => c.Id == claimId)
            .Select(c => c.IsSuperAdminOnly)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
