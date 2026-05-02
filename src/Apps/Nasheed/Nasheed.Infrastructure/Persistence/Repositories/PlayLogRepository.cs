using Microsoft.EntityFrameworkCore;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class PlayLogRepository : IPlayLogRepository
{
    private readonly NasheedDbContext _context;

    public PlayLogRepository(NasheedDbContext context) => _context = context;

    public async Task AddAsync(PlayLogEntity log, CancellationToken cancellationToken = default)
    {
        await _context.PlayLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
