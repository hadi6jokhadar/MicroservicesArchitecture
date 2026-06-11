using IhsanDev.Shared.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Category.Application.Events;
using Category.Application.Helpers;
using Category.Domain.Interfaces;
using Category.Infrastructure.Persistence;
using Category.Infrastructure.Persistence.Repositories;
using Category.Infrastructure.Services;

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

        // Event publisher — Outbox pattern:
        //   OutboxCategoryEventPublisher (Scoped) writes to the DB outbox table.
        //   Hangfire recurring job (OutboxEventProcessorJob) reads the table and publishes
        //   to Redis Pub/Sub — replaces the previous BackgroundService polling loop.
        //   Falls back to no-op when Redis is disabled so local dev still works.
        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled", false);
        if (redisEnabled)
        {
            services.AddScoped<ICategoryEventPublisher, OutboxCategoryEventPublisher>();
            services.AddCategoryHangfire(configuration);
        }
        else
            services.AddScoped<ICategoryEventPublisher, NoOpCategoryEventPublisher>();

        return services;
    }
}
