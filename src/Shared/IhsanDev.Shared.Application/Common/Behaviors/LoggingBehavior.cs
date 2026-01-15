using MediatR;
using System.Diagnostics;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Application.Localization;

namespace IhsanDev.Shared.Application.Common.Behaviors;

/// <summary>
/// Logs all MediatR requests with execution time using custom logger
/// Localizes exception messages for consistent multilingual logging
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILoggerManager _loggerManager;
    private readonly ITraceIdProvider _traceIdProvider;
    private readonly ILocalizationService _localizationService;

    public LoggingBehavior(
        ILoggerManager loggerManager, 
        ITraceIdProvider traceIdProvider,
        ILocalizationService localizationService)
    {
        _loggerManager = loggerManager;
        _traceIdProvider = traceIdProvider;
        _localizationService = localizationService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        
        // Get the SAME traceId that's used in HTTP responses
        var traceId = _traceIdProvider.GetTraceId();

        // Use custom logger for enhanced logging with traceId
        _loggerManager.LogInfo($"Handling {requestName}", "MediatR", traceId);

        try
        {
            var response = await next();

            stopwatch.Stop();
            _loggerManager.LogInfo(
                $"Handled {requestName} in {stopwatch.ElapsedMilliseconds}ms", 
                "MediatR",
                traceId);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Don't log full stack traces for expected business exceptions
            if (ex is AppException appException)
            {
                // Localize the exception message for logging (same as HTTP responses)
                var localizedMessage = _localizationService.GetString(appException.Message);
                _loggerManager.LogWarn(
                    $"Business exception in {requestName} after {stopwatch.ElapsedMilliseconds}ms: {localizedMessage}",
                    "MediatR",
                    traceId);
            }
            else
            {
                _loggerManager.LogError(
                    ex,
                    $"Unexpected error handling {requestName} after {stopwatch.ElapsedMilliseconds}ms",
                    "MediatR",
                    traceId);
            }
            
            throw;
        }
    }
}