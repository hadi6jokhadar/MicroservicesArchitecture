using Tenant.Domain.Entities;
using Tenant.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using IhsanDev.Shared.Infrastructure.Persistence;
using Tenant.Infrastructure.Persistence;

namespace Tenant.Infrastructure.Repositories;

public class TenantRepository : Repository<TenantSettings>, ITenantRepository
{
    public TenantRepository(TenantDbContext context) : base(context)
    {
    }

    public async Task<TenantSettings?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsArchived, cancellationToken);
    }

    public async Task<TenantSettings?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && !t.IsArchived, cancellationToken);
    }

    public async Task<(IEnumerable<TenantSettings> Items, int TotalCount)> GetAllActiveAsync(
        int pageNumber = 1,
        int pageSize = 10,
        bool? isArchived = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(t => t.IsActive);

        // Apply isArchived filter if specified
        if (isArchived.HasValue)
        {
            query = query.Where(t => t.IsArchived == isArchived.Value);
        }

        query = query.OrderByDescending(t => t.Created);

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> IsActiveTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && t.IsActive && !t.IsArchived, cancellationToken);
    }

    public async Task<bool> TenantIdExistsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && !t.IsArchived, cancellationToken);
    }

    public async Task<bool> UserHasTenantAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(t => t.UserId == userId && !t.IsArchived, cancellationToken);
    }

    // DeleteAsync for soft delete by setting IsArchived = true
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null)
        {
            entity.IsArchived = true;
            entity.LastModified = DateTime.UtcNow;
            _dbSet.Update(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
