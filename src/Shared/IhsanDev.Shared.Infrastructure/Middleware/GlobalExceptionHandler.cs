using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;

namespace IhsanDev.Shared.Infrastructure.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly ILocalizationService _localizationService;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, ILocalizationService localizationService)
    {
        _logger = logger;
        _localizationService = localizationService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Use different log levels for different exception types
        if (exception is AppException appException)
        {
            _logger.LogWarning(
                "Business exception occurred: {Message} | Path: {Path} | Method: {Method} | StatusCode: {StatusCode}",
                appException.Message,
                httpContext.Request.Path,
                httpContext.Request.Method,
                appException.StatusCode);
        }
        else
        {
            _logger.LogError(
                exception,
                "Unexpected exception occurred: {Message} | Path: {Path} | Method: {Method}",
                exception.Message,
                httpContext.Request.Path,
                httpContext.Request.Method);
        }

        var problemDetails = CreateProblemDetails(exception, httpContext, _localizationService);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(Exception exception, HttpContext httpContext, ILocalizationService localizationService)
    {
        return exception switch
        {
            AppException appException => new ProblemDetails
            {
                Status = appException.StatusCode,
                Title = localizationService.GetString(appException.Title),
                Detail = localizationService.GetString(appException.Message),
                Instance = httpContext.Request.Path,
                Extensions = new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier
                }
            },

            // FluentValidation errors
            ValidationException validationException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = localizationService.GetString(LocalizationKeys.Exceptions.BadRequest),
                Detail = localizationService.GetString(LocalizationKeys.Exceptions.ValidationError),
                Instance = httpContext.Request.Path,
                Extensions = new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier,
                    ["errors"] = validationException.Errors
                        .GroupBy(e => ToCamelCase(e.PropertyName))
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray())
                }
            },

            // Standard exceptions
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = localizationService.GetString(LocalizationKeys.Exceptions.Unauthorized),
                Detail = exception.Message,
                Instance = httpContext.Request.Path,
                Extensions = new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier
                }
            },

            KeyNotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = localizationService.GetString(LocalizationKeys.Exceptions.NotFound),
                Detail = exception.Message,
                Instance = httpContext.Request.Path,
                Extensions = new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier
                }
            },

            InvalidOperationException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = localizationService.GetString(LocalizationKeys.Exceptions.BadRequest),
                Detail = exception.Message,
                Instance = httpContext.Request.Path,
                Extensions = new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier
                }
            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = localizationService.GetString(LocalizationKeys.Exceptions.InternalServerError),
                Detail = localizationService.GetString(LocalizationKeys.Exceptions.UnexpectedError),
                Instance = httpContext.Request.Path,
                Extensions = new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier
                }
            }
        };
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;

        return char.ToLowerInvariant(str[0]) + str[1..];
    }
}