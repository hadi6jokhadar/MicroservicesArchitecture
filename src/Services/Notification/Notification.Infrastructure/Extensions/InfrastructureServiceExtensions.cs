using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Services;
using Notification.Domain.Repositories;
using Notification.Infrastructure.Repositories;
using Notification.Infrastructure.Services;

namespace Notification.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register repositories (following Identity service pattern)
        services.AddScoped<INotificationQueueRepository, NotificationQueueRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        
        // Register services
        services.AddScoped<INotificationService, NotificationService>();
        
        return services;
    }
}
