using System.Net;
using System.Text.Json;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Global exception handling middleware with localization support
/// Catches all unhandled exceptions and returns localized error responses
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ILocalizationService localizationService)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, localizationService);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context, 
        Exception exception, 
        ILocalizationService localizationService)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = exception switch
        {
            AppException appException => new ErrorResponse
            {
                StatusCode = appException.StatusCode,
                Title = localizationService.GetString(appException.Title),
                Message = localizationService.GetString(appException.LocalizationKey),
                LocalizationKey = appException.LocalizationKey,
                TraceId = context.TraceIdentifier
            },
            _ => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Title = localizationService.GetString(LocalizationKeys.Exceptions.InternalServerError),
                Message = localizationService.GetString(LocalizationKeys.Exceptions.InternalServerError),
                LocalizationKey = LocalizationKeys.Exceptions.InternalServerError,
                TraceId = context.TraceIdentifier
            }
        };

        response.StatusCode = errorResponse.StatusCode;

        // Log the exception
        LogException(exception, errorResponse);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(errorResponse, options);
        await response.WriteAsync(json);
    }

    private void LogException(Exception exception, ErrorResponse errorResponse)
    {
        var logLevel = errorResponse.StatusCode >= 500 
            ? LogLevel.Error 
            : LogLevel.Warning;

        _logger.Log(logLevel, exception,
            "Error occurred: StatusCode={StatusCode}, Message={Message}, TraceId={TraceId}",
            errorResponse.StatusCode,
            errorResponse.Message,
            errorResponse.TraceId);
    }

    private class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string LocalizationKey { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}
