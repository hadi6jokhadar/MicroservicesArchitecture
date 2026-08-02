using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Translation.Application.Interfaces;
using Translation.Domain.Repositories;
using Translation.Infrastructure.Repositories;
using Translation.Infrastructure.Services;

namespace Translation.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register repositories
        services.AddScoped<ITranslationKeyRepository, TranslationKeyRepository>();
        services.AddScoped<ITranslationValueRepository, TranslationValueRepository>();

        services.AddSingleton<ITranslationCacheInvalidator, TranslationCacheInvalidator>();

        return services;
    }
}
