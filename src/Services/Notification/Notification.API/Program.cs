using System.Text;
using FluentValidation;
using Hangfire;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Commands;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Extensions;
using Notification.API.BackgroundServices;
using Notification.API.Extensions;
using Notification.API.Hubs;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Infrastructure.Attributes;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Infrastructure.Services;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Enums;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Shared Services
// ============================================
var applicationAssembly = typeof(SendNotificationCommand).Assembly;

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(applicationAssembly); // Handlers are in Application layer
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(applicationAssembly);
builder.Services.AddGlobalExceptionHandler();

// ============================================
// Localization
// ============================================
builder.Services.AddLocalizationService();

// ============================================
// Custom Logging
// ============================================
builder.Services.AddCustomLogging(builder.Configuration, "Notification");

// ============================================
// Observability (OpenTelemetry → Jaeger + Prometheus)
// ============================================
builder.Services.AddPlatformObservability(builder.Configuration, "NotificationService");

// ============================================
// Multi-Tenancy Support (Optional)
// ============================================
builder.Services.AddMultiTenancy(builder.Configuration);

// ============================================
// Database Configuration
// ============================================
// Global database for queue management
builder.Services.AddDbContext<NotificationDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["DatabaseSettings:ConnectionString"];
    var provider = configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

    if (provider == "PostgreSql")
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(NotificationDbContext).Assembly.GetName().Name);
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
        });
    }
    else if (provider == "Sqlite")
    {
        options.UseSqlite(connectionString);
    }
});

// Tenant-specific database for notification history
builder.Services.AddDbContext<TenantNotificationDbContext>((serviceProvider, options) =>
{
    // Configuration will be done dynamically in OnConfiguring based on tenant context
}, ServiceLifetime.Scoped);

// Database migration service
builder.Services.AddDatabaseMigration();

// ============================================
// Authentication & Authorization
// ============================================
builder.Services.AddJwtAuthentication(
    builder.Configuration,
    enablePerTenantJwt: true,
    customMessageReceived: context =>
    {
        var path = context.HttpContext.Request.Path;
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        // Check if this is a SignalR hub request
        if (path.StartsWithSegments("/hubs/notifications"))
        {
            // Extract token from query string (required for SignalR WebSocket connections)
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
                logger.LogInformation("SignalR Hub: Token received from query string");
            }
            
            // Extract tenant ID from query string for SignalR
            var tenantId = context.Request.Headers["x-tenant-id"].FirstOrDefault()
                ?? context.Request.Query["tenantId"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(tenantId))
            {
                logger.LogInformation("SignalR Hub: Tenant user connection (TenantId: {TenantId})", tenantId);
            }
            else
            {
                logger.LogInformation("SignalR Hub: Global user connection (no tenantId)");
            }
        }

        return Task.CompletedTask;
    });

// ============================================
// CORS Configuration
// ============================================
// CORS will use tenant-specific origins when multi-tenancy is enabled
// Fallback to appsettings.json when multi-tenancy is disabled or tenant doesn't have CORS config
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ============================================
// SignalR Configuration (Optimized for 100k+ connections)
// ============================================
var signalRBuilder = builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Configuration.GetValue<bool>("SignalR:EnableDetailedErrors", false);
    options.ClientTimeoutInterval = TimeSpan.Parse(
        builder.Configuration["SignalR:ClientTimeoutInterval"] ?? "00:02:00");
    options.KeepAliveInterval = TimeSpan.Parse(
        builder.Configuration["SignalR:KeepAliveInterval"] ?? "00:00:30");
    
    // Optimizations for high-scale (100k+ connections)
    options.MaximumReceiveMessageSize = builder.Configuration.GetValue<long?>("SignalR:MaximumReceiveMessageSize") ?? 102400; // 100KB
    options.StreamBufferCapacity = builder.Configuration.GetValue<int>("SignalR:StreamBufferCapacity", 10);
    options.MaximumParallelInvocationsPerClient = 1; // Prevent client abuse
});

// Add Redis backplane for horizontal scaling (if enabled)
var redisEnabled = builder.Configuration.GetValue<bool>("Redis:Enabled", false);
if (redisEnabled)
{
    var redisConnection = builder.Configuration["Redis:ConnectionString"];
    
    if (!string.IsNullOrEmpty(redisConnection))
    {
        signalRBuilder.AddStackExchangeRedis(redisOptions =>
        {
            redisOptions.Configuration.EndPoints.Add(redisConnection.Split(',')[0]);
            
            // Parse connection options
            var connectionParts = redisConnection.Split(',');
            foreach (var part in connectionParts.Skip(1))
            {
                if (part.Trim().StartsWith("abortConnect=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = part.Split('=')[1].Trim();
                    redisOptions.Configuration.AbortOnConnectFail = bool.Parse(value);
                }
                else if (part.Trim().StartsWith("password=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = part.Split('=')[1].Trim();
                    redisOptions.Configuration.Password = value;
                }
                else if (part.Trim().StartsWith("ssl=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = part.Split('=')[1].Trim();
                    redisOptions.Configuration.Ssl = bool.Parse(value);
                }
            }
            
            // Set channel prefix for SignalR messages
            redisOptions.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("SignalR");
        });
        
        Console.WriteLine($"SignalR Redis backplane configured with connection: {redisConnection}");
    }
    else
    {
        Console.WriteLine("WARNING: Redis is enabled but connection string is missing. SignalR running without backplane (single instance only)");
    }
}
else
{
    Console.WriteLine("INFO: Redis is disabled. SignalR running without backplane (single instance only)");
}

// Configure SignalR hub authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

// ============================================
// Response Compression (Performance Optimization)
// ============================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" }); // For SignalR
});

// ============================================
// Rate Limiting (DoS Protection)
// ============================================
builder.Services.AddRateLimiter(options =>
{
    // Gateway handles Global + PerIP — services apply PerTenant/PerUser only

    // Per-Tenant rate limiting (for multi-tenant scenarios)
    options.AddPolicy("PerTenant", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Request.Headers["x-tenant-id"].FirstOrDefault() ?? "default",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PerTenant:PermitLimit", 1000),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimiting:PerTenant:WindowMinutes", 1)),
                QueueLimit = 50
            }));

    // Per-User rate limiting (for authenticated requests)
    options.AddPolicy("PerUser", context =>
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PerUser:PermitLimit", 500),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimiting:PerUser:WindowMinutes", 1)),
                QueueLimit = 20
            });
    });

    // Rejection status code
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // On rejected - log the rate limit violation
    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var endpoint = context.HttpContext.GetEndpoint()?.DisplayName ?? "Unknown";
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var tenantId = context.HttpContext.Request.Headers["x-tenant-id"].FirstOrDefault() ?? "None";
        
        logger.LogWarning("Rate limit exceeded - Endpoint: {Endpoint}, IP: {IP}, TenantId: {TenantId}", 
            endpoint, ip, tenantId);

        var localizationService = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = localizationService.GetString(LocalizationKeys.Error.RateLimitExceeded),
            message = localizationService.GetString(LocalizationKeys.Error.RateLimitExceeded),
            retryAfter = context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter)
                ? retryAfter.TotalSeconds
                : 60
        }, cancellationToken);
    };
});

// ============================================
// Services
// ============================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Memory cache for caching device tokens and other data
builder.Services.AddMemoryCache();

// Register infrastructure services (repositories, services, etc.)
builder.Services.AddInfrastructureServices();
builder.Services.AddAuditService();
builder.Services.AddAuditLogQueries<NotificationDbContext>();

// ============================================
// Service-to-Service HTTP Clients
// ============================================
// Register Identity service client for service-to-service communication (for device tokens)
builder.Services.AddIdentityServiceClient<Notification.Application.Interfaces.IIdentityServiceClient, Notification.Infrastructure.Services.IdentityServiceClient>(
    builder.Configuration,
    "NotificationService",
    builder.Environment.IsDevelopment());

// Register Tenant service client for service-to-service communication (for global notifications)
builder.Services.AddTenantServiceClient<Notification.Application.Interfaces.ITenantServiceClient, Notification.Infrastructure.Services.TenantServiceClient>(
    builder.Configuration,
    "NotificationService",
    builder.Environment.IsDevelopment());

// Firebase Cloud Messaging Service
builder.Services.AddSingleton<Notification.Application.Interfaces.IFirebaseService, Notification.Infrastructure.Services.FirebaseService>();

// ============================================
// Background Services
// ============================================
// NotificationProcessor: real-time queue poller — stays as BackgroundService (sub-second loop).
builder.Services.AddHostedService<NotificationProcessor>();
// CleanupService: hourly scheduled job — migrated to Hangfire.
builder.Services.AddNotificationHangfire(builder.Configuration);

// ============================================
// Application Services
// ============================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Notification Service API", Version = "v1" });
    
    // JWT Authentication in Swagger
    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your token"
    });
    
    options.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    
    // Add x-tenant-id header parameter for all endpoints
    options.OperationFilter<IhsanDev.Shared.Infrastructure.Filters.TenantHeaderOperationFilter>();
});

// ============================================
// Health Checks
// ============================================
var healthCheckEnabled = builder.Configuration.GetValue<bool>("DatabaseSettings:HealthCheckEnabled", true);
if (healthCheckEnabled)
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            connectionString: builder.Configuration["DatabaseSettings:ConnectionString"]!,
            name: "notification-global-database",
            tags: new[] { "database", "postgresql", "global" },
            timeout: TimeSpan.FromSeconds(5))
        .AddCheck(
            name: "notification-service",
            () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Notification service is running"),
            tags: new[] { "service" });
}

// ============================================
// Build & Configure Pipeline
// ============================================
var app = builder.Build();

// ============================================
// Log Startup Configuration
// ============================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();
if (healthCheckEnabled)
{
    logger.LogInformation("Health checks enabled for database monitoring and automatic failover");
}
logger.LogInformation("========================================");
logger.LogInformation("Notification API Starting...");
logger.LogInformation("========================================");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("URLs: {Urls}", builder.Configuration["Urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "Not Set");
logger.LogInformation("Database: {Database}", builder.Configuration["DatabaseSettings:ConnectionString"]?.Split(';').FirstOrDefault(s => s.StartsWith("Database="))?.Replace("Database=", "") ?? "Unknown");
logger.LogInformation("Multi-Tenancy: {Enabled}", builder.Configuration["MultiTenancy:Enabled"]);
logger.LogInformation("JWT Mode: {JwtMode}", builder.Configuration["MultiTenancy:JwtMode"]);
logger.LogInformation("========================================");

// NotificationProcessor and CleanupService (BackgroundServices) start at the same time
// as the HTTP server, before any HTTP request triggers UseDefaultDatabaseMigration. Migrate
// both DB contexts eagerly so the schema exists when background services start polling.
// NotificationDbContext → global queue DB (NotificationQueue + AuditLogs)
// TenantNotificationDbContext → global fallback DB (Notifications + AuditLogs); per-tenant DBs
//   are still lazily migrated on each tenant's first request via UseTenantDatabaseMigration.
await app.Services.InitializeDatabaseAsync<NotificationDbContext>(
    applyMigrations: true,
    seedData: false);
await app.Services.InitializeDatabaseAsync<TenantNotificationDbContext>(
    applyMigrations: true,
    seedData: false);

// Enable Swagger in all environments (for debugging)
// TODO: Restrict to Development only in production
app.UseSwagger();
app.UseSwaggerUI();

app.UseCorrelationId();

// Localization middleware (must be before exception handler)
app.UseLocalization();

app.UseGlobalExceptionHandler();
app.UseResponseCompression(); // Enable response compression for better network performance

// Rate limiting (must be early in pipeline)
app.UseRateLimiter();

// Only redirect to HTTPS in production (disabled in development for easier testing)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Migrate global DBs BEFORE tenant resolution so the global fallback connection
// string is used. NotificationDbContext (global queue) must be migrated here — not just
// in UseTenantDatabaseMigration — because background services query it before any HTTP
// request triggers the tenant-based path.
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false);
app.UseDefaultDatabaseMigration<NotificationDbContext>();
app.UseDefaultDatabaseMigration<TenantNotificationDbContext>();

// Multi-tenancy middleware (must be before CORS and authentication)
// Only runs if MultiTenancy:Enabled is true
app.UseTenantResolution(builder.Configuration);

// Tenant-aware CORS (validates origins based on tenant config or appsettings)
// Must be after tenant resolution to access tenant context
// MUST be BEFORE JwtTenantVerification to handle OPTIONS preflight requests first
// This middleware handles both preflight (OPTIONS) and actual requests
app.UseTenantAwareCors();

// JWT tenant verification (AFTER tenant resolution and CORS, BEFORE authentication)
// Prevents users from accessing other tenants by changing x-tenant-id header
app.UseJwtTenantVerification(builder.Configuration);

// Note: Standard UseCors() is NOT needed because TenantAwareCors handles everything
// DO NOT call app.UseCors() - it will conflict with TenantAwareCorsMiddleware

if (multiTenancyEnabled)
{
    // Also run per-tenant migration on each tenant's first request.
    // NotificationDbContext → global queue (idempotent, always same DB regardless of tenant)
    // TenantNotificationDbContext → per-tenant notification history
    app.UseTenantDatabaseMigration<NotificationDbContext>(builder.Configuration);
    app.UseTenantDatabaseMigration<TenantNotificationDbContext>(builder.Configuration);
}

// Service authentication middleware (must be BEFORE UseAuthentication)
// Allows service-to-service communication with shared secret
app.UseServiceAuthentication();

app.UseAuthentication();
app.UseAuthorization();

// ============================================
// API Endpoints
// ============================================
app.MapNotificationEndpoints();
app.MapAuditLogEndpoints();

// Hangfire dashboard + recurring jobs
app.UseNotificationHangfireDashboard(app.Configuration);
HangfireExtensions.RegisterNotificationRecurringJobs();

// ============================================
// SignalR Hub Endpoint
// ============================================
// Authentication is optional - hub handles both authenticated and anonymous connections
// Tenant is optional - hub can work with or without tenant context
app.MapHub<NotificationHub>("/hubs/notifications")
    .WithMetadata(new OptionalTenantAttribute());
    // OptionalTenant: Middleware will set tenant context if x-tenant-id is provided,
    // but won't fail if missing. This allows hub to work in all scenarios:
    // 1. No tenant + no token => global only
    // 2. Tenant + no token => global + tenant
    // 3. Tenant + token => all notifications

// ============================================
// Health Check
// ============================================
if (healthCheckEnabled)
{
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            });
            await context.Response.WriteAsync(result);
        }
    })
    .WithName("DetailedHealthCheck")
    .AllowAnonymous();
    
    // Simple health check for load balancers
    app.MapHealthChecks("/health/ready")
        .WithName("ReadinessCheck")
        .AllowAnonymous();
}
else
{
    // Fallback simple health check
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "notification" }))
        .WithName("HealthCheck")
        .AllowAnonymous();
}

// ============================================
// Start Application
// ============================================
logger.LogInformation("========================================");
logger.LogInformation("Notification API Started Successfully!");
logger.LogInformation("========================================");

app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();

// Make Program class accessible to tests
public partial class Program { }
