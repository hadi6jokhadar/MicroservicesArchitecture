using Tenant.Domain.Repositories;
using Tenant.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Tenant.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<ITenantRepository, TenantRepository>();
        
        return services;
    }
}
