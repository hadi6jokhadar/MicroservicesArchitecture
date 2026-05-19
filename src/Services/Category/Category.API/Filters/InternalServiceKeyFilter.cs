using Microsoft.Extensions.Configuration;

namespace Category.API.Filters;

/// <summary>
/// Endpoint filter that rejects requests missing a valid <c>x-internal-service-key</c> header.
/// Configure the expected key in <c>appsettings.json</c> under <c>InternalServices:ApiKey</c>.
///
/// Usage:
/// <code>
/// endpoint.AddEndpointFilter&lt;InternalServiceKeyFilter&gt;();
/// </code>
/// </summary>
public sealed class InternalServiceKeyFilter : IEndpointFilter
{
    private const string HeaderName = "x-internal-service-key";

    private readonly string? _expectedKey;

    public InternalServiceKeyFilter(IConfiguration configuration)
    {
        _expectedKey = configuration["InternalServices:ApiKey"];
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (string.IsNullOrWhiteSpace(_expectedKey))
        {
            // Key not configured — block access to prevent accidental exposure
            return Results.Problem(
                title: "Internal endpoint not configured",
                detail: "InternalServices:ApiKey is not set. Contact the service operator.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedKey)
            || providedKey != _expectedKey)
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
