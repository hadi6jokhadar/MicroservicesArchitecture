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
        return _httpContextAccessor.HttpContext?.TraceIdentifier;
    }
}
