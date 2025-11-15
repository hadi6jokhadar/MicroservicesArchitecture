using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Database;
using IhsanDev.Shared.Kernel.Interfaces.Database;

namespace IhsanDev.Shared.Infrastructure.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Configures DbContext with the specified database provider
    /// Supports multi-tenancy: If tenant has custom database settings, they will be used via OnConfiguring
    /// Otherwise, falls back to appsettings.json configuration
    /// </summary>
    public static IServiceCollection AddDatabaseContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string? migrationAssembly = null) 
        where TContext : DbContext
    {
        // Register DatabaseSettings as IOptions<DatabaseSettings>
        services.Configure<DatabaseSettings>(
            configuration.GetSection(DatabaseSettings.SectionName));

        // Check if multi-tenancy is enabled
        var multiTenancyEnabled = configuration.GetValue<bool>("MultiTenancy:Enabled", false);

        if (multiTenancyEnabled)
        {
            // When multi-tenancy is enabled, register DbContext with minimal configuration
            // The actual database connection will be resolved in OnConfiguring based on tenant context
            services.AddDbContext<TContext>(ServiceLifetime.Scoped);
        }
        else
        {
            // Traditional approach: Configure DbContext at startup with static connection string
            services.AddDbContext<TContext>((serviceProvider, options) =>
            {
                var dbSettings = serviceProvider
                    .GetRequiredService<IOptions<DatabaseSettings>>()
                    .Value;

                // Validate connection string
                if (string.IsNullOrWhiteSpace(dbSettings.ConnectionString))
                {
                    throw new InvalidOperationException(
                        $"Connection string is not configured in {DatabaseSettings.SectionName}");
                }

                ConfigureDbContext(options, dbSettings, migrationAssembly, serviceProvider);
            });
        }

        return services;
    }

    private static void ConfigureDbContext(
        DbContextOptionsBuilder options,
        DatabaseSettings settings,
        string? migrationAssembly,
        IServiceProvider serviceProvider)
    {
        // Common configurations
        if (settings.EnableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging();
        }

        if (settings.EnableDetailedErrors)
        {
            options.EnableDetailedErrors();
        }

        // Provider-specific configuration
        switch (settings.Provider)
        {
            case DatabaseProvider.PostgreSql:
                // Configure Npgsql to handle all DateTimes as UTC
                // This ensures DateTime values from PostgreSQL are always treated as UTC
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
                
                options.UseNpgsql(
                    settings.ConnectionString,
                    npgsqlOptions =>
                    {
                        if (!string.IsNullOrEmpty(migrationAssembly))
                        {
                            npgsqlOptions.MigrationsAssembly(migrationAssembly);
                        }
                        
                        npgsqlOptions.CommandTimeout(settings.CommandTimeout);
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: settings.MaxRetryCount,
                            maxRetryDelay: TimeSpan.FromSeconds(settings.MaxRetryDelay),
                            errorCodesToAdd: null);
                        
                        // Modern PostgreSQL features
                        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                break;

            case DatabaseProvider.Sqlite:
                options.UseSqlite(
                    settings.ConnectionString,
                    sqliteOptions =>
                    {
                        if (!string.IsNullOrEmpty(migrationAssembly))
                        {
                            sqliteOptions.MigrationsAssembly(migrationAssembly);
                        }
                        
                        sqliteOptions.CommandTimeout(settings.CommandTimeout);
                    });
                break;

            default:
                throw new NotSupportedException($"Database provider '{settings.Provider}' is not supported");
        }

        // Use logger factory from DI
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        if (loggerFactory != null)
        {
            options.UseLoggerFactory(loggerFactory);
        }
    }

    /// <summary>
    /// Applies pending migrations and seeds data (Development only)
    /// Modern approach: Async initialization with retry logic
    /// </summary>
    public static async Task InitializeDatabaseAsync<TContext>(
        this IServiceProvider serviceProvider,
        bool applyMigrations = true,
        bool seedData = false) 
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

        try
        {
            if (applyMigrations)
            {
                logger.LogInformation("Applying database migrations for {Context}...", typeof(TContext).Name);
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully");
            }

            if (seedData)
            {
                logger.LogInformation("Seeding database for {Context}...", typeof(TContext).Name);
                // Call seed method if exists
                var seedMethod = context.GetType().GetMethod("SeedAsync");
                if (seedMethod != null)
                {
                    await (Task)seedMethod.Invoke(context, null)!;
                }
                logger.LogInformation("Database seeded successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the database");
            throw;
        }
    }

    /// <summary>
    /// Add database migration service to the service collection
    /// Required for automatic database migration
    /// </summary>
    public static IServiceCollection AddDatabaseMigration(
        this IServiceCollection services)
    {
        services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
        return services;
    }

    /// <summary>
    /// Add automatic database migration middleware for default database
    /// This ensures the default database from appsettings.json is automatically created and migrated on first request
    /// Use this when multi-tenancy is disabled or you want to ensure the default database exists
    /// Should be called BEFORE UseAuthentication()
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to migrate</typeparam>
    public static IApplicationBuilder UseDefaultDatabaseMigration<TContext>(
        this IApplicationBuilder app)
        where TContext : DbContext
    {
        return app.UseMiddleware<DefaultDatabaseMigrationMiddleware<TContext>>();
    }
}