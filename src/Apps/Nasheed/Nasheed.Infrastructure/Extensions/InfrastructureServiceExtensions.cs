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

        // AI API Client (HTTP) with standard resilience (3 retries + circuit breaker)
        services.AddHttpClient<IAiApiClient, AiApiClientService>(client =>
        {
            var baseUrl = configuration["Services:AiService:BaseUrl"]
                ?? "http://localhost:5008";
            client.BaseAddress = new Uri(baseUrl);
        }).AddStandardResilienceHandler();

        // Tenant cache singleton — holds the single tenant's DB config at runtime
        services.AddSingleton<INasheedTenantCache, NasheedTenantCache>();

        // Loader fetches tenant config from TenantService on startup (must run before the worker)
        services.AddHostedService<NasheedTenantLoaderService>();

        // Ingestion background worker — waits for INasheedTenantCache to be ready
        services.AddHostedService<NasheedIngestionWorker>();

        return services;
    }
}
