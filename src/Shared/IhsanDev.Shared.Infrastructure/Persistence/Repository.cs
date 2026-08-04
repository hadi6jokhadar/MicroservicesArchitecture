using Microsoft.EntityFrameworkCore;
using IhsanDev.Shared.Kernel.Entities;

namespace IhsanDev.Shared.Infrastructure.Persistence;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly DbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsArchived, cancellationToken);
    }

    public virtual async Task<T?> GetByIdWithArchivedAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public virtual IQueryable<T> GetAll()
    {
        return _dbSet.AsNoTracking().Where(e => !e.IsArchived);
    }

    public virtual IQueryable<T> GetAllWithArchived()
    {
        return _dbSet.AsNoTracking();
    }

    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(e => !e.IsArchived, cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.LastModified = DateTime.UtcNow;
        AttachOrMerge(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.IsArchived = true;
        entity.LastModified = DateTime.UtcNow;
        AttachOrMerge(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Attaches <paramref name="entity"/> as Modified, unless a different instance with the
    /// same key is already tracked by this DbContext (e.g. a caller fetched it via a
    /// no-tracking query like <see cref="GetByIdWithArchivedAsync"/> after the same request
    /// already loaded/saved the tracked instance elsewhere) — in that case <c>_dbSet.Update</c>
    /// would throw "the instance ... cannot be tracked because another instance with the same
    /// key value ... is already being tracked". When that happens, copy the incoming values
    /// onto the already-tracked instance instead of attaching a second one for the same key.
    /// </summary>
    private void AttachOrMerge(T entity)
    {
        var tracked = _context.ChangeTracker.Entries<T>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id && !ReferenceEquals(e.Entity, entity));

        if (tracked != null)
        {
            tracked.CurrentValues.SetValues(entity);
        }
        else
        {
            _dbSet.Update(entity);
        }
    }

    public virtual async Task<bool> DeleteByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null) return false;
        
        return await DeleteAsync(entity, cancellationToken);
    }

    public virtual async Task<bool> HardDeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}