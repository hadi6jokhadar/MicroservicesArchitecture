using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Translation.Domain.Entities;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;

namespace Translation.Infrastructure.Persistence;

/// <summary>
/// Translation Service uses a GLOBAL database (not multi-tenant)
/// All tenants' translations are stored in a single database with TenantId column for isolation
/// This is similar to Tenant Service pattern - it's a provider of translations, not a consumer
/// </summary>
public class TranslationDbContext : BaseDbContext
{
    private readonly IConfiguration? _configuration;
    private readonly ILogger<TranslationDbContext>? _logger;

    public TranslationDbContext(
        DbContextOptions<TranslationDbContext> options, 
        ICurrentUserService? currentUserService = null,
        IConfiguration? configuration = null,
        ILogger<TranslationDbContext>? logger = null) 
        : base(options, currentUserService)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    public DbSet<TranslationKey> TranslationKeys => Set<TranslationKey>();
    public DbSet<TranslationValue> TranslationValues => Set<TranslationValue>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // If already configured (from DI), skip
        if (optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
            return;
        }

        // Translation Service ALWAYS uses the global database from appsettings.json
        // Even when multi-tenancy is enabled, it uses one database for all tenants
        if (_configuration != null)
        {
            var connectionString = _configuration.GetValue<string>("DatabaseSettings:ConnectionString");
            var provider = _configuration.GetValue<string>("DatabaseSettings:Provider");

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                _logger?.LogDebug("Configuring TranslationDbContext with global database connection");

                if (provider?.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) == true)
                {
                    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
                    optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(typeof(TranslationDbContext).Assembly.GetName().Name);
                        npgsqlOptions.CommandTimeout(_configuration.GetValue<int>("DatabaseSettings:CommandTimeout", 30));
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: _configuration.GetValue<int>("DatabaseSettings:MaxRetryCount", 3),
                            maxRetryDelay: TimeSpan.FromSeconds(_configuration.GetValue<int>("DatabaseSettings:MaxRetryDelay", 30)),
                            errorCodesToAdd: null);
                    });
                }
                else if (provider?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
                {
                    optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
                    {
                        sqliteOptions.MigrationsAssembly(typeof(TranslationDbContext).Assembly.GetName().Name);
                        sqliteOptions.CommandTimeout(_configuration.GetValue<int>("DatabaseSettings:CommandTimeout", 30));
                    });
                }

                if (_configuration.GetValue<bool>("DatabaseSettings:EnableSensitiveDataLogging", false))
                {
                    optionsBuilder.EnableSensitiveDataLogging();
                }

                if (_configuration.GetValue<bool>("DatabaseSettings:EnableDetailedErrors", false))
                {
                    optionsBuilder.EnableDetailedErrors();
                }
            }
        }

        base.OnConfiguring(optionsBuilder);
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<TranslationKey>(entity =>
        {
            entity.ToTable("TranslationKeys");
            entity.HasKey(e => e.Id);
            
            // Unique constraint on Key
            entity.HasIndex(e => e.Key)
                .IsUnique()
                .HasDatabaseName("IX_TranslationKeys_Key");
            
            // Index on Category for filtering
            entity.HasIndex(e => e.Category)
                .HasDatabaseName("IX_TranslationKeys_Category");
            
            // Index on IsActive for filtering
            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_TranslationKeys_IsActive");
            
            entity.Property(e => e.Key)
                .HasMaxLength(200)
                .IsRequired();
            
            entity.Property(e => e.Category)
                .HasMaxLength(100)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasMaxLength(500);
        });
        
        modelBuilder.Entity<TranslationValue>(entity =>
        {
            entity.ToTable("TranslationValues");
            entity.HasKey(e => e.Id);
            
            // Composite unique index: One value per key+language+tenant
            entity.HasIndex(e => new { e.TranslationKeyId, e.Language, e.TenantId })
                .IsUnique()
                .HasDatabaseName("IX_TranslationValues_Key_Lang_Tenant");
            
            // Index for tenant queries (important for performance)
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_TranslationValues_TenantId");
            
            // Index for language queries
            entity.HasIndex(e => e.Language)
                .HasDatabaseName("IX_TranslationValues_Language");
            
            entity.Property(e => e.Language)
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.Value)
                .IsRequired();
            
            entity.Property(e => e.TenantId)
                .HasMaxLength(450);
            
            // Navigation property configuration
            entity.HasOne(e => e.TranslationKey)
                .WithMany(k => k.Values)
                .HasForeignKey(e => e.TranslationKeyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
