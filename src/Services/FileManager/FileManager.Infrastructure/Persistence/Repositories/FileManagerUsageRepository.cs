using FileManager.Domain.Entities;
using FileManager.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FileManager.Infrastructure.Persistence.Repositories;

public class FileManagerUsageRepository : IFileManagerUsageRepository
{
    private readonly FileManagerDbContext _context;

    public FileManagerUsageRepository(FileManagerDbContext context)
    {
        _context = context;
    }

    public async Task<FileManagerUsageEntity?> GetUsageAsync(
        int fileId,
        string usageArea,
        string rowId,
        CancellationToken cancellationToken = default)
    {
        return await _context.FileManagerUsage
            .FirstOrDefaultAsync(
                u => u.FileId == fileId && u.UsageArea == usageArea && u.RowId == rowId,
                cancellationToken);
    }

    public async Task<int> CountUsagesAsync(int fileId, CancellationToken cancellationToken = default)
    {
        return await _context.FileManagerUsage
            .CountAsync(u => u.FileId == fileId, cancellationToken);
    }

    public async Task AddAsync(FileManagerUsageEntity usage, CancellationToken cancellationToken = default)
    {
        _context.FileManagerUsage.Add(usage);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(FileManagerUsageEntity usage, CancellationToken cancellationToken = default)
    {
        _context.FileManagerUsage.Remove(usage);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
