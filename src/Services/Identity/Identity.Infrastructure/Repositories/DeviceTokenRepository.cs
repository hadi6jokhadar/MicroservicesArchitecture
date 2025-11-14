using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using IhsanDev.Shared.Kernel.Entities;
using IhsanDev.Shared.Kernel.Enums;
using IhsanDev.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class DeviceTokenRepository : Repository<DeviceToken>, IDeviceTokenRepository
{
    public DeviceTokenRepository(IdentityDbContext context) : base(context)
    {
    }

    public async Task<List<DeviceToken>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsArchived)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.Created)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token && !x.IsArchived, cancellationToken);
    }

    public async Task<List<DeviceToken>> GetByUserIdAndPlatformAsync(int userId, Platform platform, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Platform == platform && !x.IsArchived)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.Created)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _dbSet
            .Where(x => x.UserId == userId && !x.IsArchived)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsArchived = true;
            token.LastModified = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(x => x.Token == token && !x.IsArchived, cancellationToken);
    }

    public async Task<List<DeviceToken>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => !x.IsArchived)
            .OrderByDescending(x => x.Created)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DeviceToken>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        // The tenant context is already set by middleware, so this query
        // will automatically filter to the current tenant's database
        return await _dbSet
            .AsNoTracking()
            .Where(x => !x.IsArchived)
            .OrderByDescending(x => x.Created)
            .ToListAsync(cancellationToken);
    }
}
