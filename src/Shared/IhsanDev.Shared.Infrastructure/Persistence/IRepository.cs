namespace IhsanDev.Shared.Infrastructure.Persistence;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<T?> GetByIdWithArchivedAsync(int id, CancellationToken cancellationToken = default);
    IQueryable<T> GetAll();
    IQueryable<T> GetAllWithArchived();
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> HardDeleteAsync(T entity, CancellationToken cancellationToken = default);
}