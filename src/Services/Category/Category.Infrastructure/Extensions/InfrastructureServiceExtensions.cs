using IhsanDev.Shared.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Category.Application.Helpers;
using Category.Domain.Interfaces;
using Category.Infrastructure.Persistence;
using Category.Infrastructure.Persistence.Repositories;

namespace Category.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDatabaseContext<CategoryDbContext>(
            configuration,
            migrationAssembly: typeof(CategoryDbContext).Assembly.GetName().Name);

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<CategoryFileManagerHelper>();
        services.AddFileManagerServiceClient(configuration, "CategoryService");

        return services;
    }
}
