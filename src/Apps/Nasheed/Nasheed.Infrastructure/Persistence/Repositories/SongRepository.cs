using IhsanDev.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class SongRepository : Repository<SongEntity>, ISongRepository
{
    public SongRepository(NasheedDbContext context) : base(context) { }

    public override async Task<SongEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _dbSet
            .Include(s => s.Artist)
            .Include(s => s.MoodTags)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsArchived, cancellationToken);

    public async Task<(List<SongEntity> Items, int TotalCount)> GetAllAsync(
        string? textFilter = null,
        int? artistId = null,
        SongState? state = null,
        string? copyrightRiskLevel = null,
        string? contentSafetyFlag = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(s => s.Artist)
            .Where(e => !e.IsArchived);

        if (!string.IsNullOrWhiteSpace(textFilter))
        {
            var escaped = textFilter.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(e => EF.Functions.Like(e.Title, $"%{escaped}%", "\\"));
        }

        if (artistId.HasValue)
            query = query.Where(e => e.ArtistId == artistId.Value);

        if (state.HasValue)
            query = query.Where(e => e.SongState == state.Value);

        if (!string.IsNullOrWhiteSpace(copyrightRiskLevel))
        {
            var normalizedRiskLevel = copyrightRiskLevel.Trim().ToLowerInvariant();
            query = query.Where(e => e.LegalCompliance != null && e.LegalCompliance.CopyrightRiskLevel == normalizedRiskLevel);
        }

        if (!string.IsNullOrWhiteSpace(contentSafetyFlag))
        {
            var normalizedSafetyFlag = contentSafetyFlag.Trim().ToLowerInvariant();
            query = query.Where(e => e.LegalCompliance != null && e.LegalCompliance.ContentSafetyFlag == normalizedSafetyFlag);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<SongEntity>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(s => s.Artist)
            .Where(e => ids.Contains(e.Id) && !e.IsArchived)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SongEntity>> GetByArtistIdAsync(int artistId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => e.ArtistId == artistId && !e.IsArchived)
            .ToListAsync(cancellationToken);
    }
}
