using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using IhsanDev.Shared.Application.Common.Behaviors;

namespace IhsanDev.Shared.Application.Extensions;

public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers MediatR with validation pipeline behavior
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        Assembly assembly)
    {
        // MediatR with validation pipeline
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        // AutoMapper
        services.AddAutoMapper(assembly);

        return services;
    }
}