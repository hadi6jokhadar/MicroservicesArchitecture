using Tenant.Domain.Repositories;
using Tenant.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using IhsanDev.Shared.Infrastructure.Extensions;

namespace Tenant.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register repositories
        services.AddScoped<ITenantRepository, TenantRepository>();

        // Register cache service (Redis or in-memory based on configuration)
        services.AddCacheService(configuration);

        services.AddTenantHangfire(configuration);

        return services;
    }
}
