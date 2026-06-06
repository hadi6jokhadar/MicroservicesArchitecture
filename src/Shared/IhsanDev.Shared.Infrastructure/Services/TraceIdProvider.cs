using IhsanDev.Shared.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace IhsanDev.Shared.Infrastructure.Services;

/// <summary>
/// Provides access to the current HTTP request's trace identifier
/// </summary>
public class TraceIdProvider : ITraceIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TraceIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetTraceId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return null;

        // Prefer X-Correlation-Id so logs can be grepped by the same ID the client sees in the response header.
        // Fall back to Kestrel's TraceIdentifier when the correlation middleware hasn't run (e.g. health checks).
        return context.Items["CorrelationId"]?.ToString()
               ?? context.TraceIdentifier;
    }
}
