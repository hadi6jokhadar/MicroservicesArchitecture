using MediatR;
using System.Diagnostics;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;

namespace IhsanDev.Shared.Application.Common.Behaviors;

/// <summary>
/// Logs all MediatR requests with execution time using custom logger
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILoggerManager _loggerManager;

    public LoggingBehavior(ILoggerManager loggerManager)
    {
        _loggerManager = loggerManager;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        // Use custom logger for enhanced logging
        _loggerManager.LogInfo($"Handling {requestName}", "MediatR");

        try
        {
            var response = await next();

            stopwatch.Stop();
            _loggerManager.LogInfo(
                $"Handled {requestName} in {stopwatch.ElapsedMilliseconds}ms", 
                "MediatR");

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Don't log full stack traces for expected business exceptions
            if (ex is AppException appException)
            {
                _loggerManager.LogWarn(
                    $"Business exception in {requestName} after {stopwatch.ElapsedMilliseconds}ms: {appException.Message}",
                    "MediatR");
            }
            else
            {
                _loggerManager.LogError(
                    ex,
                    $"Unexpected error handling {requestName} after {stopwatch.ElapsedMilliseconds}ms",
                    "MediatR");
            }
            
            throw;
        }
    }
}