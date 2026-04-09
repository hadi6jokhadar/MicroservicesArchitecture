using System.Collections.Generic;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using IhsanDev.Shared.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence;

namespace Identity.Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(IdentityDbContext context) : base(context)
    {
    }

    public override async Task<User> UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        // Temporarily null out UserRoles to prevent EF Core from attaching the entire
        // Role/RoleClaim graph during a simple User update, which causes tracking exceptions.
        var originalRoles = entity.UserRoles;
        entity.UserRoles = null!;
        
        var result = await base.UpdateAsync(entity, cancellationToken);
        
        entity.UserRoles = originalRoles;
        return result;
    }

    public override async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AsSplitQuery()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RoleClaims)
                        .ThenInclude(rc => rc.Claim)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsArchived, cancellationToken);
    }

    public async Task<User?> GetByIdWithArchivedAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AsSplitQuery()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RoleClaims)
                        .ThenInclude(rc => rc.Claim)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AsSplitQuery()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RoleClaims)
                        .ThenInclude(rc => rc.Claim)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsArchived, cancellationToken);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AsSplitQuery()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RoleClaims)
                        .ThenInclude(rc => rc.Claim)
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && !u.IsArchived, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(u => u.Email == email && !u.IsArchived, cancellationToken);
    }

    public async Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AsSplitQuery()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RoleClaims)
                        .ThenInclude(rc => rc.Claim)
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && !u.IsArchived, cancellationToken);
    }

    public async Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;
            
        return await _dbSet.AnyAsync(u => u.PhoneNumber == phoneNumber && !u.IsArchived, cancellationToken);
    }

    public IQueryable<User> GetUsersByRoleName(string roleName)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();
        return _dbSet
            .AsNoTracking()
            .Where(u => !u.IsArchived)
            .Where(u => u.UserRoles.Any(ur => ur.Role.NormalizedName == normalizedRoleName || ur.Role.Name == roleName));
    }

    public IQueryable<User> GetUsersByRoleNameWithArchived(string roleName)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();
        return _dbSet
            .AsNoTracking()
            .Where(u => u.UserRoles.Any(ur => ur.Role.NormalizedName == normalizedRoleName || ur.Role.Name == roleName));
    }

    public async Task<bool> UpdateRefreshTokenAsync(int userId, string refreshToken, DateTime expiryTime, CancellationToken cancellationToken = default)
    {
        var user = await _dbSet.FirstOrDefaultAsync(u => u.Id == userId && !u.IsArchived, cancellationToken);
        if (user == null) return false;

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = expiryTime;
        user.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RevokeRefreshTokenAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbSet.FirstOrDefaultAsync(u => u.Id == userId && !u.IsArchived, cancellationToken);
        if (user == null) return false;

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        user.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateLastLoginAsync(int userId, CancellationToken cancellationToken = default)
    {
        await _dbSet.Where(u => u.Id == userId && !u.IsArchived)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LastLogin, DateTime.UtcNow)
                .SetProperty(u => u.LastModified, DateTime.UtcNow), 
            cancellationToken);
    }
}