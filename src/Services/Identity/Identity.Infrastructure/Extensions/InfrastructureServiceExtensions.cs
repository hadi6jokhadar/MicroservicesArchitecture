using Identity.Application.Services;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using IhsanDev.Shared.Infrastructure.Services.Otp;
using IhsanDev.Shared.Infrastructure.Services.Notification;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRoleClaimRepository, RoleClaimRepository>();
        
        // Register services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<DatabaseSeeder>();
        
        // Register OTP service (from Shared)
        services.AddScoped<IOtpService, OtpService>();
        
        // Register Notification Service Client (for service-to-service communication)
        services.AddScoped<INotificationServiceClient, NotificationServiceClient>();
        
        return services;
    }
}