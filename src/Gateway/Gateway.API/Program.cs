using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// YARP Reverse Proxy
// ============================================
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ============================================
// Rate Limiting
// ============================================
builder.Services.AddRateLimiter(options =>
{
    // Global: total request cap across all clients — gateway-wide protection
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetFixedWindowLimiter("global", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10_000,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        }));

    // PerIP: only the gateway sees the real client IP — services see the gateway's loopback address
    options.AddPolicy("per-ip", context =>
    {
        var ip = context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
        return RateLimitPartition.GetFixedWindowLimiter(ip.ToString(), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 500,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// ============================================
// Correlation ID injection
// ============================================
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.Headers.ContainsKey("X-Correlation-Id"))
        ctx.Request.Headers.Append("X-Correlation-Id", Guid.NewGuid().ToString());
    await next();
});

// ============================================
// Health check (lightweight — no downstream calls)
// ============================================
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Gateway.API", timestamp = DateTimeOffset.UtcNow }))
   .AllowAnonymous();

app.UseRateLimiter();

// RequireRateLimiting("per-ip") enforces the named per-IP policy on every proxied request.
// The GlobalLimiter runs automatically; named policies only fire when explicitly required.
app.MapReverseProxy().RequireRateLimiting("per-ip");

app.Run();
