using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// YARP Reverse Proxy
// ============================================
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Per-route "Timeout" values in appsettings (e.g. ai-stream-route) are enforced by the
// ASP.NET Core Request Timeouts middleware — YARP just reads the policy, it doesn't apply it.
// DefaultPolicy covers every other route, which previously relied solely on Kestrel's
// minimum-data-rate heuristics with no explicit ceiling.
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(100)
    };
});

// ============================================
// Rate Limiting
// ============================================
// ============================================
// HttpClient for downstream health checks
// ============================================
builder.Services.AddHttpClient("downstream-health", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Token bucket, not fixed window: a fixed window lets a client spend its whole quota in the
// last instant of one window and again in the first instant of the next (up to 2x the intended
// rate in a short burst). A token bucket absorbs real traffic spikes smoothly instead — burst up
// to TokenLimit immediately, then throttle to a steady TokensPerPeriod/ReplenishmentPeriod rate.
int GetInt(string key, int fallback) => builder.Configuration.GetValue(key, fallback);

var globalTokenLimit = GetInt("RateLimiting:Global:TokenLimit", 20_000);
var globalTokensPerPeriod = GetInt("RateLimiting:Global:TokensPerPeriod", 5_000);
var globalReplenishSeconds = GetInt("RateLimiting:Global:ReplenishmentSeconds", 1);

var perIpTokenLimit = GetInt("RateLimiting:PerIp:TokenLimit", 200);
var perIpTokensPerPeriod = GetInt("RateLimiting:PerIp:TokensPerPeriod", 50);
var perIpReplenishSeconds = GetInt("RateLimiting:PerIp:ReplenishmentSeconds", 1);

// Auth gets its own, separate per-IP bucket so a burst of unrelated API traffic from a shared
// NAT/office IP can never exhaust the budget that login/register/refresh depend on.
var authTokenLimit = GetInt("RateLimiting:PerIpAuth:TokenLimit", 20);
var authTokensPerPeriod = GetInt("RateLimiting:PerIpAuth:TokensPerPeriod", 5);
var authReplenishSeconds = GetInt("RateLimiting:PerIpAuth:ReplenishmentSeconds", 1);

// /health/aggregate fans out to all 9 downstream services per hit, so an unauthenticated flood
// multiplies 9x downstream — it needs its own generous-but-nonzero budget rather than the plain
// .DisableRateLimiting() the lightweight /health endpoint uses.
var healthAggregateTokenLimit = GetInt("RateLimiting:HealthAggregate:TokenLimit", 60);
var healthAggregateTokensPerPeriod = GetInt("RateLimiting:HealthAggregate:TokensPerPeriod", 20);
var healthAggregateReplenishSeconds = GetInt("RateLimiting:HealthAggregate:ReplenishmentSeconds", 1);

builder.Services.AddRateLimiter(options =>
{
    // Global: total request cap across all clients — platform-wide circuit breaker against a
    // runaway or catastrophic-overload scenario, not meant to bottleneck legitimate scale-up.
    // Applies to every request through UseRateLimiter(), including endpoints with no named
    // policy attached — the lightweight /health endpoint explicitly opts out via
    // .DisableRateLimiting() below so LB/k8s probes never compete with real traffic.
    // /health/aggregate does NOT opt out — see the "health-aggregate" policy below.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetTokenBucketLimiter("global", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = globalTokenLimit,
            TokensPerPeriod = globalTokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(globalReplenishSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    // PerIP: only the gateway sees the real client IP — services see the gateway's loopback address.
    // Auth routes are partitioned into their own bucket per IP (separate token count) so general
    // API traffic can never starve login/register/refresh of their budget.
    options.AddPolicy("per-ip", context =>
    {
        var ip = context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
        var isAuthPath = context.Request.Path.StartsWithSegments("/api/v1/auth");
        var partitionKey = $"{ip}:{(isAuthPath ? "auth" : "api")}";

        var (tokenLimit, tokensPerPeriod, replenishSeconds) = isAuthPath
            ? (authTokenLimit, authTokensPerPeriod, authReplenishSeconds)
            : (perIpTokenLimit, perIpTokensPerPeriod, perIpReplenishSeconds);

        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimit,
            TokensPerPeriod = tokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(replenishSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    // Named, per-IP policy for /health/aggregate — unauthenticated but bounded, unlike
    // .DisableRateLimiting(), so a flood can no longer turn into an unbounded 9-way fan-out.
    options.AddPolicy("health-aggregate", context =>
    {
        var ip = context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
        return RateLimitPartition.GetTokenBucketLimiter(ip.ToString(), _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = healthAggregateTokenLimit,
            TokensPerPeriod = healthAggregateTokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(healthAggregateReplenishSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true
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
    // Always overwrite — a client-supplied value here would pass straight through to every
    // backend's logs/traces unverified, letting a client forge or collide correlation IDs.
    ctx.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();
    await next();
});

// ============================================
// Health check (lightweight — gateway only)
// ============================================
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Gateway.API", timestamp = DateTimeOffset.UtcNow }))
   .AllowAnonymous()
   .DisableRateLimiting();

// ============================================
// Aggregate health check — calls all downstream /health endpoints in parallel
// ============================================
app.MapGet("/health/aggregate", async (IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    // Read cluster addresses from YARP config so this endpoint stays in sync with routing
    var serviceUrls = configuration.GetSection("ReverseProxy:Clusters")
        .GetChildren()
        .Select(cluster => new
        {
            Name = cluster.Key,
            HealthUrl = (cluster["Destinations:d1:Address"] ?? "").TrimEnd('/') + "/health"
        })
        .Where(s => !string.IsNullOrEmpty(s.HealthUrl.Replace("/health", "")))
        .ToList();

    var client = httpClientFactory.CreateClient("downstream-health");

    var tasks = serviceUrls.Select(async s =>
    {
        try
        {
            var response = await client.GetAsync(s.HealthUrl);
            return (s.Name, status: response.IsSuccessStatusCode ? "healthy" : "unhealthy");
        }
        catch
        {
            return (s.Name, status: "unreachable");
        }
    });

    var results = await Task.WhenAll(tasks);
    var overall = results.All(r => r.status == "healthy") ? "healthy" : "degraded";

    return Results.Ok(new
    {
        status = overall,
        gateway = "healthy",
        timestamp = DateTimeOffset.UtcNow,
        services = results.ToDictionary(r => r.Name, r => r.status)
    });
}).AllowAnonymous()
  .RequireRateLimiting("health-aggregate");

app.UseRateLimiter();

// Public-facing listener only — internal YARP destinations stay http:// on the Docker network
// (see Clusters in appsettings.json). Gated the same way every other service in this repo gates
// it (see Notification.API's Program.cs) so local dev over http:// is unaffected.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Must sit between routing and endpoint execution — required for any YARP route that sets
// a "Timeout" value (see ai-stream-route in appsettings.json), or YARP throws at request time.
app.UseRequestTimeouts();

// RequireRateLimiting("per-ip") enforces the named per-IP policy on every proxied request.
// The GlobalLimiter runs automatically; named policies only fire when explicitly required.
app.MapReverseProxy().RequireRateLimiting("per-ip");

app.Run();
