using System.Text;
using FluentValidation;
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
// Read JWT mode configuration to determine if JWT is shared or per-tenant
var jwtModeString = builder.Configuration["MultiTenancy:JwtMode"] ?? "Shared";
var jwtMode = Enum.TryParse<JwtMode>(jwtModeString, ignoreCase: true, out var parsedMode)
    ? parsedMode
    : JwtMode.Shared;

// Use global JWT settings from appsettings.json as the default
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JWT Secret is not configured");

// Store default validation parameters (used for global JWT and as base for tenant-specific JWT)
var defaultValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
    ValidateIssuer = true,
    ValidIssuer = jwtSettings["Issuer"],
    ValidateAudience = true,
    ValidAudience = jwtSettings["Audience"],
    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero
};

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = defaultValidationParameters.Clone();

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var path = context.HttpContext.Request.Path;
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            // Reset to default validation parameters for each request to prevent cross-request pollution
            context.Options.TokenValidationParameters = defaultValidationParameters.Clone();

            // Extract tenant ID from header or query string
            var tenantId = context.Request.Headers["x-tenant-id"].FirstOrDefault()
                ?? context.Request.Query["tenantId"].FirstOrDefault();

            // Determine if endpoint should use tenant-specific JWT
            var shouldUseTenantJwt = false;

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
                
                // If tenantId is provided, use tenant-specific JWT; otherwise use global JWT
                if (!string.IsNullOrEmpty(tenantId))
                {
                    shouldUseTenantJwt = true;
                    logger.LogInformation("SignalR Hub: Tenant user connection (TenantId: {TenantId})", tenantId);
                }
                else
                {
                    logger.LogInformation("SignalR Hub: Global user connection (no tenantId)");
                }
            }
            // Check if this is a user endpoint (requires tenant context)
            else if (path.StartsWithSegments("/api/notifications/user") || 
                     (path.StartsWithSegments("/api/notifications/") && path.Value?.Contains("/read") == true))
            {
                // User endpoints require tenant-specific JWT when JwtMode is PerTenant
                shouldUseTenantJwt = !string.IsNullOrEmpty(tenantId);
                logger.LogInformation("User endpoint detected: {Path}, TenantId: {TenantId}, ShouldUseTenantJwt: {ShouldUseTenantJwt}", 
                    path, tenantId ?? "null", shouldUseTenantJwt);
            }

            // Apply tenant-specific JWT validation when needed and JwtMode is PerTenant
            if (shouldUseTenantJwt && jwtMode == JwtMode.PerTenant && !string.IsNullOrEmpty(tenantId))
            {
                var tenantConfigProvider = context.HttpContext.RequestServices.GetService<ITenantConfigurationProvider>();
                if (tenantConfigProvider != null)
                {
                    try
                    {
                        var tenant = tenantConfigProvider.GetTenantConfigurationAsync(tenantId, context.HttpContext.RequestAborted)
                            .GetAwaiter().GetResult();
                        
                        if (tenant?.Configuration?.Jwt != null)
                        {
                            var tenantJwt = tenant.Configuration.Jwt;
                            if (!string.IsNullOrEmpty(tenantJwt.Secret))
                            {
                                // Override with tenant-specific JWT validation parameters
                                context.Options.TokenValidationParameters = new TokenValidationParameters
                                {
                                    ValidateIssuer = true,
                                    ValidateAudience = true,
                                    ValidateLifetime = true,
                                    ValidateIssuerSigningKey = true,
                                    ValidIssuer = tenantJwt.Issuer,
                                    ValidAudience = tenantJwt.Audience,
                                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tenantJwt.Secret)),
                                    ClockSkew = TimeSpan.Zero
                                };
                                
                                logger.LogInformation("🔐 Using tenant-specific JWT validation for tenant: {TenantId} (Issuer: {Issuer})", 
                                    tenantId, tenantJwt.Issuer);
                            }
                        }
                        else
                        {
                            logger.LogWarning("Tenant {TenantId} has no JWT configuration, using global JWT", tenantId);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to fetch tenant configuration for tenant: {TenantId}, falling back to global JWT", tenantId);
                        // Keep default global JWT parameters (already set above)
                    }
                }
            }
            else if (!string.IsNullOrEmpty(tenantId))
            {
                logger.LogInformation("Using global JWT validation (JwtMode: {JwtMode})", jwtMode);
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var path = context.HttpContext.Request.Path;
            logger.LogInformation("JWT Token Validated - User ID: {UserId}, Path: {Path}", userId ?? "Unknown", path);
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var path = context.HttpContext.Request.Path;
            logger.LogError("JWT Authentication Failed - Path: {Path}, Error: {Error}", path, context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();

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
    // Global rate limit across all requests
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "global",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:Global:PermitLimit", 10000),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimiting:Global:WindowMinutes", 1)),
                QueueLimit = 0
            }));

    // Per-IP rate limiting
    options.AddPolicy("PerIP", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PerIP:PermitLimit", 100),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimiting:PerIP:WindowMinutes", 1)),
                QueueLimit = 10
            }));

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

// HTTP Client for Identity Service (for device tokens)
var isDevEnv = builder.Environment.IsDevelopment();
builder.Services.AddHttpClient<Notification.Application.Interfaces.IIdentityServiceClient, Notification.Infrastructure.Services.IdentityServiceClient>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("IdentityService:BaseUrl")
        ?? throw new InvalidOperationException("IdentityService:BaseUrl is not configured");
    
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    var serviceSecret = builder.Configuration.GetValue<string>("ServiceCommunication:SharedSecret");
    if (!string.IsNullOrEmpty(serviceSecret))
    {
        client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
        client.DefaultRequestHeaders.Add("X-Service-Name", "NotificationService");
    }
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (isDevEnv)
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// HTTP Client for Tenant Service (for global notifications)
builder.Services.AddHttpClient<Notification.Application.Interfaces.ITenantServiceClient, Notification.Infrastructure.Services.TenantServiceClient>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("MultiTenancy:TenantServiceUrl")
        ?? throw new InvalidOperationException("MultiTenancy:TenantServiceUrl is not configured");
    
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    var serviceSecret = builder.Configuration.GetValue<string>("ServiceCommunication:SharedSecret");
    if (!string.IsNullOrEmpty(serviceSecret))
    {
        client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
        client.DefaultRequestHeaders.Add("X-Service-Name", "NotificationService");
    }
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (isDevEnv)
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// Firebase Cloud Messaging Service
builder.Services.AddSingleton<Notification.Application.Interfaces.IFirebaseService, Notification.Infrastructure.Services.FirebaseService>();

// ============================================
// Background Services
// ============================================
builder.Services.AddHostedService<NotificationProcessor>();
builder.Services.AddHostedService<CleanupService>();

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

// Initialize database (Development only)
// Skip if multi-tenancy is enabled - tenant databases will be initialized automatically per-request
if (app.Environment.IsDevelopment() && !builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false))
{
    await app.Services.InitializeDatabaseAsync<NotificationDbContext>(
        applyMigrations: true,
        seedData: false);
}

// Enable Swagger in all environments (for debugging)
// TODO: Restrict to Development only in production
app.UseSwagger();
app.UseSwaggerUI();

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

// Multi-tenancy middleware (must be before CORS and authentication)
// Only runs if MultiTenancy:Enabled is true
app.UseTenantResolution(builder.Configuration);

// Tenant-aware CORS (validates origins based on tenant config or appsettings)
// Must be after tenant resolution to access tenant context
// This middleware handles both preflight (OPTIONS) and actual requests
app.UseTenantAwareCors();

// Note: Standard UseCors() is NOT needed because TenantAwareCors handles everything
// DO NOT call app.UseCors() - it will conflict with TenantAwareCorsMiddleware

// Automatic database migration - use EITHER tenant or default based on configuration
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false);
if (multiTenancyEnabled)
{
    // Multi-tenancy enabled: Use tenant database migration
    // NotificationDbContext → Global queue database (shared across all tenants)
    // TenantNotificationDbContext → Each tenant's notification history database
    app.UseTenantDatabaseMigration<NotificationDbContext>(builder.Configuration);
    app.UseTenantDatabaseMigration<TenantNotificationDbContext>(builder.Configuration);
}
else
{
    // Multi-tenancy disabled: Use default database migration
    // Both contexts use the same global database from appsettings.json
    // NotificationDbContext → Global queue database
    // TenantNotificationDbContext → Global notification history (same DB, TenantNotificationDbContext.OnConfiguring handles fallback)
    app.UseDefaultDatabaseMigration<NotificationDbContext>();
    app.UseDefaultDatabaseMigration<TenantNotificationDbContext>();
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

app.Run();

// Make Program class accessible to tests
public partial class Program { }
