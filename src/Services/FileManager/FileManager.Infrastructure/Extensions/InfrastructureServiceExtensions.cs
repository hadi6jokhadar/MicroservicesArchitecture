using FileManager.Application.Interfaces;
using FileManager.Domain.Interfaces;
using FileManager.Infrastructure.Persistence;
using FileManager.Infrastructure.Persistence.Repositories;
using FileManager.Infrastructure.Services;
using FileManager.Infrastructure.Storage;
using IhsanDev.Shared.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileManager.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabaseContext<FileManagerDbContext>(
            configuration,
            migrationAssembly: typeof(FileManagerDbContext).Assembly.GetName().Name);

        services.AddScoped<IFileManagerRepository, FileManagerRepository>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IFileManagerService, FileManagerService>();
        
        services.AddHostedService<FileManager.Infrastructure.BackgroundJobs.TempFileCleanupService>();

        return services;
    }
}
