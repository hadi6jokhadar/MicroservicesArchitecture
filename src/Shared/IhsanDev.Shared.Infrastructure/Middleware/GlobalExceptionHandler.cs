using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Application.Exceptions;

namespace IhsanDev.Shared.Infrastructure.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Exception occurred: {Message} | Path: {Path} | Method: {Method}",
            exception.Message,
            httpContext.Request.Path,
            httpContext.Request.Method);

        var problemDetails = CreateProblemDetails(exception, httpContext);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(Exception exception, HttpContext httpContext)
    {
        return exception switch
        {
            AppException appException => new ProblemDetails
            {
                Status = appException.StatusCode,
                Title = appException.Title,
                Detail = appException.Message,
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
                Title = "Validation Error",
                Detail = "One or more validation errors occurred",
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
                Title = "Unauthorized",
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
                Title = "Resource Not Found",
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
                Title = "Bad Request",
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
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred. Please try again later.",
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