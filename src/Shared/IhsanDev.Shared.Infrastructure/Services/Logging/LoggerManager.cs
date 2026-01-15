using IhsanDev.Shared.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;

namespace IhsanDev.Shared.Infrastructure.Services.Logging;

/// <summary>
/// Custom logger manager implementation with file logging and console output
/// </summary>
public class LoggerManager : ILoggerManager
{
    private readonly ILogger<LoggerManager> _logger;
    private readonly string _projectLogFilePath;
    private readonly object _lockObject = new();

    public LoggerManager(ILogger<LoggerManager> logger, string projectLogFilePath)
    {
        _logger = logger;
        _projectLogFilePath = projectLogFilePath;
        
        // Ensure the directory exists
        if (!string.IsNullOrWhiteSpace(_projectLogFilePath))
        {
            Directory.CreateDirectory(_projectLogFilePath);
        }
    }

    public void LogInfo(string message, string? serviceName = null, string? traceId = null)
    {
        Log(LogLevel.Information, message, serviceName, null, traceId);
    }

    public void LogError(string message, string? serviceName = null, string? traceId = null)
    {
        Log(LogLevel.Error, message, serviceName, null, traceId);
    }

    public void LogError(Exception exception, string message, string? serviceName = null, string? traceId = null)
    {
        Log(LogLevel.Error, $"{message} | Exception: {exception.Message}", serviceName, exception, traceId);
    }

    public void LogDebug(string message, string? serviceName = null, string? traceId = null)
    {
        Log(LogLevel.Debug, message, serviceName, null, traceId);
    }

    public void LogWarn(string message, string? serviceName = null, string? traceId = null)
    {
        Log(LogLevel.Warning, message, serviceName, null, traceId);
    }

    private void Log(LogLevel logLevel, string message, string? serviceName = null, Exception? exception = null, string? traceId = null)
    {
        var contextualMessage = serviceName != null ? $"[{serviceName}] {message}" : message;
        
        // Set Console Colors
        ConfigureConsoleColors(logLevel);

        // Log to Console
        Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [{logLevel}] {contextualMessage}");

        // Reset Console Colors
        Console.ResetColor();

        // Log using the built-in ILogger for structured logging
        if (exception != null)
        {
            _logger.Log(logLevel, exception, contextualMessage);
        }
        else
        {
            _logger.Log(logLevel, contextualMessage);
        }

        // Log to file if path is configured
        if (!string.IsNullOrWhiteSpace(_projectLogFilePath))
        {
            WriteLogToFile(logLevel, contextualMessage, exception, traceId);
        }
    }

    private void WriteLogToFile(LogLevel logLevel, string message, Exception? exception = null, string? traceId = null)
    {
        try
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var filePath = Path.Combine(_projectLogFilePath, $"project-{date}.log");
            var logMessage = FormatLogMessage(logLevel, message, exception, traceId);

            lock (_lockObject) // Synchronize file access
            {
                using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                writer.WriteLine(logMessage);
            }
        }
        catch (IOException ex)
        {
            // Fallback to console if file logging fails
            Console.WriteLine($"Failed to write log to file: {ex.Message}");
        }
    }

    private static string FormatLogMessage(LogLevel logLevel, string message, Exception? exception = null, string? traceId = null)
    {
        var traceIdPart = !string.IsNullOrWhiteSpace(traceId) ? $" | TraceId: {traceId}" : string.Empty;
        var logEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}]{traceIdPart} {message}";
        
        if (exception != null)
        {
            logEntry += $"{Environment.NewLine}Exception: {exception}{Environment.NewLine}";
        }
        
        return logEntry;
    }

    private static void ConfigureConsoleColors(LogLevel logLevel)
    {
        Console.ForegroundColor = logLevel switch
        {
            LogLevel.Information => ConsoleColor.Green,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Debug => ConsoleColor.Blue,
            LogLevel.Critical => ConsoleColor.Magenta,
            _ => ConsoleColor.White
        };
    }
}