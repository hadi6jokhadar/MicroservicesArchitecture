using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nasheed.Domain.Entities;
using Nasheed.Infrastructure.Services;

namespace Nasheed.Infrastructure.Persistence;

public class NasheedDbContext : BaseDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly INasheedTenantCache? _nasheedTenantCache;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<NasheedDbContext>? _logger;

    public NasheedDbContext(
        DbContextOptions<NasheedDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null,
        INasheedTenantCache? nasheedTenantCache = null,
        IConfiguration? configuration = null,
        ILogger<NasheedDbContext>? logger = null)
        : base(options, currentUserService)
    {
        _tenantContext = tenantContext;
        _nasheedTenantCache = nasheedTenantCache;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<ArtistEntity> Artists => Set<ArtistEntity>();
    public DbSet<SongEntity> Songs => Set<SongEntity>();
    public DbSet<SongMoodTagEntity> SongMoodTags => Set<SongMoodTagEntity>();
    public DbSet<PlayLogEntity> PlayLogs => Set<PlayLogEntity>();
    public DbSet<FavoriteEntity> Favorites => Set<FavoriteEntity>();
    public DbSet<RatingEntity> Ratings => Set<RatingEntity>();
    public DbSet<SongIngestionJobEntity> SongIngestionJobs => Set<SongIngestionJobEntity>();
    public DbSet<SongSearchDocumentEntity> SongSearchDocuments => Set<SongSearchDocumentEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
            return;
        }

        string connectionString;
        string provider;

        // Priority 1: HTTP request — tenant context set by UseTenantResolution middleware
        if (_tenantContext?.HasTenant == true &&
            _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings?.ConnectionString != null)
        {
            var tenantDb = _tenantContext.CurrentTenant.Configuration.DatabaseSettings;
            connectionString = tenantDb.ConnectionString!;
            provider = tenantDb.Provider ?? "PostgreSql";
            _logger?.LogDebug("Using tenant DB for tenant '{TenantId}' (request context)", _tenantContext.TenantId);
        }
        // Priority 2: Background/startup — singleton cache populated by NasheedTenantLoaderService
        else if (_nasheedTenantCache?.IsReady == true &&
                 _nasheedTenantCache.Tenant?.Configuration?.DatabaseSettings?.ConnectionString != null)
        {
            var tenantDb = _nasheedTenantCache.Tenant.Configuration.DatabaseSettings;
            connectionString = tenantDb.ConnectionString!;
            provider = tenantDb.Provider ?? "PostgreSql";
            _logger?.LogDebug("Using cached tenant DB (background context)");
        }
        // Priority 3: Design-time only — EF migrations tooling reads from appsettings.Development.json
        else if (!string.IsNullOrWhiteSpace(_configuration?["DatabaseSettings:ConnectionString"]))
        {
            connectionString = _configuration["DatabaseSettings:ConnectionString"]!;
            provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";
            _logger?.LogDebug("Using design-time DatabaseSettings from configuration (EF tooling)");
        }
        else
        {
            throw new InvalidOperationException(
                "NasheedDbContext: no database connection available. " +
                "Ensure NasheedTenantLoaderService has run or DatabaseSettings is present in appsettings.Development.json for migrations.");
        }

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
            optionsBuilder.UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly(typeof(NasheedDbContext).Assembly.GetName().Name);
                o.EnableRetryOnFailure(maxRetryCount: 3);
            });
        }
        else if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(connectionString, o =>
                o.MigrationsAssembly(typeof(NasheedDbContext).Assembly.GetName().Name));
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NasheedDbContext).Assembly);
    }
}
