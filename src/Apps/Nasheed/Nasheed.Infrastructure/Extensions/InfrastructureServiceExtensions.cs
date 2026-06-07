using IhsanDev.Shared.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Nasheed.Application.Interfaces;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;
using Nasheed.Infrastructure.Persistence.Repositories;
using Nasheed.Infrastructure.Services;
using Nasheed.Infrastructure.Workers;
using Polly;

namespace Nasheed.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDatabaseContext<NasheedDbContext>(
            configuration,
            migrationAssembly: typeof(NasheedDbContext).Assembly.GetName().Name);

        // Unit of Work
        services.AddScoped<INasheedUnitOfWork, NasheedUnitOfWork>();

        // Repositories
        services.AddScoped<IArtistRepository, ArtistRepository>();
        services.AddScoped<ISongRepository, SongRepository>();
        services.AddScoped<ISongIngestionJobRepository, SongIngestionJobRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<IPlayLogRepository, PlayLogRepository>();
        services.AddScoped<ISongMoodTagRepository, SongMoodTagRepository>();
        services.AddScoped<ISongSearchDocumentRepository, SongSearchDocumentRepository>();

        // AI API Client (HTTP) — no attempt/total-timeout overrides because model calls
        // can be long-running. The circuit breaker still protects against a dead AI service.
        services.AddHttpClient<IAiApiClient, AiApiClientService>(client =>
        {
            var baseUrl = configuration["Services:AiService:BaseUrl"]
                ?? "http://localhost:5008";
            client.BaseAddress = new Uri(baseUrl);
        })
        .AddResilienceHandler("ai-pipeline", builder =>
        {
            // Retry once with a short back-off — AI calls are expensive; don't hammer.
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Constant,
            });

            // Circuit breaker: open after 3 failures in 60s, stay open for 30s.
            builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(60),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(30),
            });
        });

        // Tenant cache singleton — holds the single tenant's DB config at runtime
        services.AddSingleton<INasheedTenantCache, NasheedTenantCache>();

        // Loader fetches tenant config from TenantService on startup (must run before the worker)
        services.AddHostedService<NasheedTenantLoaderService>();

        // Ingestion background worker — waits for INasheedTenantCache to be ready
        services.AddHostedService<NasheedIngestionWorker>();

        return services;
    }
}
