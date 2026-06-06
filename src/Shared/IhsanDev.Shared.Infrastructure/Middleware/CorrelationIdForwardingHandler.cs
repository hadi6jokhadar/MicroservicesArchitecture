using Microsoft.AspNetCore.Http;

namespace IhsanDev.Shared.Infrastructure.Middleware;

/// <summary>
/// Propagates the X-Correlation-Id header to outgoing service-to-service HTTP calls
/// so the same correlation ID appears in every service's log files.
/// </summary>
public class CorrelationIdForwardingHandler : DelegatingHandler
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();

        if (!string.IsNullOrEmpty(correlationId) &&
            !request.Headers.Contains(CorrelationIdHeader))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdHeader, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
