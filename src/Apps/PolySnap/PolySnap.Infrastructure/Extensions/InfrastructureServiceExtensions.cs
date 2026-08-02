using IhsanDev.Shared.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolySnap.Domain.Interfaces;
using PolySnap.Infrastructure.Persistence;
using PolySnap.Infrastructure.Persistence.Repositories;

namespace PolySnap.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDatabaseContext<PolySnapDbContext>(
            configuration,
            migrationAssembly: typeof(PolySnapDbContext).Assembly.GetName().Name);

        services.AddScoped<ISnapRequestRepository, SnapRequestRepository>();

        return services;
    }
}
