namespace IhsanDev.Shared.Application.Common.Interfaces;

/// <summary>
/// Provides access to the current request's trace ID for correlation
/// </summary>
public interface ITraceIdProvider
{
    /// <summary>
    /// Gets the trace ID for the current request, or null if not available
    /// </summary>
    string? GetTraceId();
}
