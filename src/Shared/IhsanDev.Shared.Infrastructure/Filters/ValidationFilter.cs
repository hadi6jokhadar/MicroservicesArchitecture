using FluentValidation;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Application.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace IhsanDev.Shared.Infrastructure.Filters;

/// <summary>
/// Generic validation filter for minimal API endpoints with localized error responses
/// Used across all microservices to provide consistent error handling
/// Logs validation failures to ensure they appear in log files
/// </summary>
public class SharedValidationFilter<T> : IEndpointFilter where T : class
{
    private readonly IValidator<T> _validator;

    public SharedValidationFilter(IValidator<T> validator)
    {
        _validator = validator;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Find the parameter of type T in the endpoint arguments
        var argumentToValidate = context.Arguments.OfType<T>().FirstOrDefault();
        
        if (argumentToValidate is not null)
        {
            var validationResult = await _validator.ValidateAsync(argumentToValidate);
            
            if (!validationResult.IsValid)
            {
                var localizationService = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
                var loggerManager = context.HttpContext.RequestServices.GetRequiredService<ILoggerManager>();
                var traceId = context.HttpContext.TraceIdentifier;
                
                // Log validation failure with traceId for tracking
                var requestType = typeof(T).Name;
                var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
                loggerManager.LogWarn(
                    $"Validation failed for {requestType}: {errors}",
                    "ValidationFilter",
                    traceId);
                
                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = localizationService.GetString(LocalizationKeys.Exceptions.BadRequest),
                    Detail = localizationService.GetString(LocalizationKeys.Exceptions.ValidationError),
                    Instance = context.HttpContext.Request.Path,
                    Extensions = new Dictionary<string, object?>
                    {
                        ["traceId"] = traceId,
                        ["errors"] = validationResult.Errors
                            .GroupBy(e => ToCamelCase(e.PropertyName))
                            .ToDictionary(
                                g => g.Key,
                                g => g.Select(e => e.ErrorMessage).ToArray())
                    }
                };

                return Results.BadRequest(problemDetails);
            }
        }

        return await next(context);
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;

        return char.ToLowerInvariant(str[0]) + str[1..];
    }
}

