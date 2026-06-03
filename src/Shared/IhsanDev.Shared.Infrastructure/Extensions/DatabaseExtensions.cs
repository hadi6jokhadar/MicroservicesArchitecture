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
            // When multi-tenancy is enabled, still register DbContext but allow OnConfiguring to resolve connection
            // The actual database connection will be resolved in OnConfiguring based on tenant context or fallback to global
            services.AddDbContext<TContext>((serviceProvider, options) =>
            {
                // Don't configure the provider here - let OnConfiguring handle it
                // This ensures optionsBuilder.IsConfigured returns false in OnConfiguring
                // which allows the DbContext to dynamically choose the connection based on tenant context
            }, ServiceLifetime.Scoped);
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
    /// Applies pending migrations and optional seed data at startup, with retry logic.
    /// Retries handle the case where multiple service instances start simultaneously and
    /// one instance fails to acquire PostgreSQL's implicit migration lock.
    /// Each retry creates a fresh scope so the DbContext is not reused after a failure.
    /// </summary>
    public static async Task InitializeDatabaseAsync<TContext>(
        this IServiceProvider serviceProvider,
        bool applyMigrations = true,
        bool seedData = false,
        int maxAttempts = 3,
        int retryDelaySeconds = 5)
        where TContext : DbContext
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(TContext).Name);

        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();

                if (applyMigrations)
                {
                    logger.LogInformation(
                        "Applying database migrations for {Context} (attempt {Attempt}/{MaxAttempts})...",
                        typeof(TContext).Name, attempt, maxAttempts);

                    await context.Database.MigrateAsync();

                    logger.LogInformation(
                        "Database migrations applied successfully for {Context}",
                        typeof(TContext).Name);
                }

                if (seedData)
                {
                    logger.LogInformation("Seeding database for {Context}...", typeof(TContext).Name);
                    var seedMethod = context.GetType().GetMethod("SeedAsync");
                    if (seedMethod != null)
                        await (Task)seedMethod.Invoke(context, null)!;
                    logger.LogInformation("Database seeded successfully for {Context}", typeof(TContext).Name);
                }

                return;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxAttempts)
                {
                    // Add per-instance jitter so concurrent instances don't all retry at the same moment
                    var jitter = Random.Shared.Next(0, retryDelaySeconds);
                    var delay = retryDelaySeconds + jitter;

                    logger.LogWarning(
                        "Database initialization attempt {Attempt}/{MaxAttempts} failed for {Context}. " +
                        "Retrying in {Delay}s... Error: {Message}",
                        attempt, maxAttempts, typeof(TContext).Name, delay, ex.Message);

                    await Task.Delay(TimeSpan.FromSeconds(delay));
                }
            }
        }

        logger.LogError(lastException,
            "Database initialization failed after {MaxAttempts} attempt(s) for {Context}",
            maxAttempts, typeof(TContext).Name);

        throw lastException!;
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