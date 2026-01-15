namespace IhsanDev.Shared.Application.Common.Interfaces;

/// <summary>
/// Custom logger manager interface for enhanced logging capabilities
/// </summary>
public interface ILoggerManager
{
    /// <summary>
    /// Log informational message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="serviceName">Optional service name for context</param>
    /// <param name="traceId">Optional trace ID for request correlation</param>
    void LogInfo(string message, string? serviceName = null, string? traceId = null);
    
    /// <summary>
    /// Log warning message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="serviceName">Optional service name for context</param>
    /// <param name="traceId">Optional trace ID for request correlation</param>
    void LogWarn(string message, string? serviceName = null, string? traceId = null);
    
    /// <summary>
    /// Log debug message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="serviceName">Optional service name for context</param>
    /// <param name="traceId">Optional trace ID for request correlation</param>
    void LogDebug(string message, string? serviceName = null, string? traceId = null);
    
    /// <summary>
    /// Log error message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="serviceName">Optional service name for context</param>
    /// <param name="traceId">Optional trace ID for request correlation</param>
    void LogError(string message, string? serviceName = null, string? traceId = null);
    
    /// <summary>
    /// Log error message with exception details
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="message">Additional message context</param>
    /// <param name="serviceName">Optional service name for context</param>
    /// <param name="traceId">Optional trace ID for request correlation</param>
    void LogError(Exception exception, string message, string? serviceName = null, string? traceId = null);
}