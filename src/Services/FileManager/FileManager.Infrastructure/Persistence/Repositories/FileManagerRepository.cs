using FileManager.Domain.Entities;
using FileManager.Domain.Enums;
using FileManager.Domain.Interfaces;
using FileManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FileManager.Infrastructure.Persistence.Repositories;

public class FileManagerRepository : IFileManagerRepository
{
    private readonly FileManagerDbContext _context;

    public FileManagerRepository(FileManagerDbContext context)
    {
        _context = context;
    }

    public async Task<FileManagerEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.FileManager
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<(List<FileManagerEntity> Items, int TotalCount)> GetAllAsync(
        int? id = null,
        bool? status = null,
        bool? isArchived = null,
        DateTime? from = null,
        DateTime? to = null,
        string? textFilter = null,
        FileGroup? group = null,
        FileType? type = null,
        bool? temp = null,
        int? userId = null,
        string sortBy = "Id",
        bool ascending = true,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.FileManager.AsQueryable();

        // Apply filters
        if (id.HasValue)
            query = query.Where(f => f.Id == id.Value);

        if (status.HasValue)
            query = query.Where(f => f.Status == status.Value);

        if (isArchived.HasValue)
            query = query.Where(f => f.IsArchived == isArchived.Value);

        if (from.HasValue)
            query = query.Where(f => f.Created >= from.Value);

        if (to.HasValue)
            query = query.Where(f => f.Created <= to.Value);

        if (!string.IsNullOrWhiteSpace(textFilter))
        {
            query = query.Where(f =>
                f.Id.ToString().Contains(textFilter) ||
                f.Name.Contains(textFilter) ||
                f.Extension.Contains(textFilter));
        }

        if (group.HasValue)
            query = query.Where(f => f.Group == group.Value);

        if (type.HasValue)
            query = query.Where(f => f.Type == type.Value);

        if (temp.HasValue)
            query = query.Where(f => f.Temp == temp.Value);

        if (userId.HasValue)
            query = query.Where(f => f.UserId == userId.Value);

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting
        query = sortBy.ToLower() switch
        {
            "name" => ascending ? query.OrderBy(f => f.Name) : query.OrderByDescending(f => f.Name),
            "extension" => ascending ? query.OrderBy(f => f.Extension) : query.OrderByDescending(f => f.Extension),
            "size" => ascending ? query.OrderBy(f => f.Size) : query.OrderByDescending(f => f.Size),
            "type" => ascending ? query.OrderBy(f => f.Type) : query.OrderByDescending(f => f.Type),
            "group" => ascending ? query.OrderBy(f => f.Group) : query.OrderByDescending(f => f.Group),
            "created" => ascending ? query.OrderBy(f => f.Created) : query.OrderByDescending(f => f.Created),
            "lastmodified" => ascending ? query.OrderBy(f => f.LastModified) : query.OrderByDescending(f => f.LastModified),
            _ => ascending ? query.OrderBy(f => f.Id) : query.OrderByDescending(f => f.Id)
        };

        // Apply pagination
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<FileManagerEntity> AddAsync(FileManagerEntity entity, CancellationToken cancellationToken = default)
    {
        _context.FileManager.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(FileManagerEntity entity, CancellationToken cancellationToken = default)
    {
        entity.LastModified = DateTime.UtcNow;
        _context.FileManager.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(FileManagerEntity entity, CancellationToken cancellationToken = default)
    {
        _context.FileManager.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteAllTempAsync(CancellationToken cancellationToken = default)
    {
        var tempFiles = await _context.FileManager
            .Where(f => f.Temp)
            .ToListAsync(cancellationToken);

        _context.FileManager.RemoveRange(tempFiles);
        await _context.SaveChangesAsync(cancellationToken);

        return tempFiles.Count;
    }

    public async Task<int> DeleteOldTempFilesAsync(int olderThanDays, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
        var oldTempFiles = await _context.FileManager
            .Where(f => f.Temp && f.Created < cutoffDate)
            .ToListAsync(cancellationToken);

        _context.FileManager.RemoveRange(oldTempFiles);
        await _context.SaveChangesAsync(cancellationToken);

        return oldTempFiles.Count;
    }

    public async Task<List<FileManagerEntity>> GetTempFilesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FileManager
            .Where(f => f.Temp)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FileManagerEntity>> GetOldTempFilesAsync(int olderThanDays, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
        return await _context.FileManager
            .Where(f => f.Temp && f.Created < cutoffDate)
            .ToListAsync(cancellationToken);
    }
}
