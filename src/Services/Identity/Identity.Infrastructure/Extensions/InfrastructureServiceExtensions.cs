using Identity.Application.Services;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        
        // Register services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        
        return services;
    }
}