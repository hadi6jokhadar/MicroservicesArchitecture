using FileManager.API.Endpoints;
using FileManager.Application.Handlers.SaveFile;
using FileManager.Application.Interfaces;
using FileManager.Domain.Interfaces;
using FileManager.Infrastructure.Options;
using FileManager.Infrastructure.Persistence;
using FileManager.Infrastructure.Persistence.Repositories;
using FileManager.Infrastructure.Services;
using FileManager.Infrastructure.Storage;
using FluentValidation;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Shared Services (Reusable across all microservices)
// ============================================
// MediatR and FluentValidation
var applicationAssembly = typeof(SaveFileCommandHandler).Assembly; // FileManager.Application assembly

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(applicationAssembly);
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
builder.Services.AddCustomLogging(builder.Configuration, "FileManager");

// ============================================
// Multi-Tenancy Support (Optional)
// ============================================
builder.Services.AddMultiTenancy(builder.Configuration);

// ============================================
// Database Configuration (Multi-Provider)
// ============================================
builder.Services.AddDatabaseContext<FileManagerDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(FileManagerDbContext).Assembly.GetName().Name);

// Add database migration service for automatic database creation
builder.Services.AddDatabaseMigration();

// ============================================
// Authentication & Authorization
// ============================================
// Read JWT mode configuration to determine if JWT is shared or per-tenant
var jwtModeString = builder.Configuration["MultiTenancy:JwtMode"] ?? "Shared";
var jwtMode = Enum.TryParse<JwtMode>(jwtModeString, ignoreCase: true, out var parsedMode) 
    ? parsedMode 
    : JwtMode.Shared;

// Always use Jwt section from appsettings.json (for both Shared and PerTenant modes)
var jwtSettings = builder.Configuration.GetSection("Jwt");

var secretKey = jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JWT Secret is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
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
    
    // Support per-tenant JWT validation when JwtMode is PerTenant
    if (jwtMode == JwtMode.PerTenant)
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var tenantId = context.HttpContext.Request.Headers["x-tenant-id"].FirstOrDefault();
                
                // Always create a fresh TokenValidationParameters to avoid cross-request contamination
                // Only attempt tenant-specific JWT validation if x-tenant-id header is present
                if (!string.IsNullOrEmpty(tenantId))
                {
                    try
                    {
                        var tenantConfigProvider = context.HttpContext.RequestServices.GetService<ITenantConfigurationProvider>();
                        if (tenantConfigProvider != null)
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
                                    return Task.CompletedTask;
                                }
                            }
                            
                            logger.LogWarning("Tenant {TenantId} has no JWT configuration, falling back to global JWT", tenantId);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to fetch tenant configuration for tenant: {TenantId}, falling back to global JWT", tenantId);
                    }
                }
                
                // Use global JWT validation (no tenant header OR tenant config fetch failed)
                logger.LogInformation("Using global JWT validation - Secret: {SecretLength} chars, Issuer: {Issuer}", 
                    secretKey.Length, jwtSettings["Issuer"]);
                
                // Explicitly set global JWT parameters
                context.Options.TokenValidationParameters = new TokenValidationParameters
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
    }
    // When JwtMode is Shared, use the JWT settings from appsettings.json
    // All tenants validate tokens using the same JWT secret from Jwt section
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

    // Per-IP rate limiting (prevent file upload/download abuse)
    options.AddPolicy("PerIP", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PerIP:PermitLimit", 50),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimiting:PerIP:WindowMinutes", 1)),
                QueueLimit = 10
            }));

    // Per-Tenant rate limiting
    options.AddPolicy("PerTenant", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Request.Headers["x-tenant-id"].FirstOrDefault() ?? "default",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PerTenant:PermitLimit", 2000),
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
// Response Compression (Performance Optimization)
// ============================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

// ============================================
// Application Services
// ============================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FileManager API",
        Version = "v1",
        Description = "File Manager Service for managing file uploads, metadata, and storage"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Add x-tenant-id header parameter for multi-tenancy
    options.OperationFilter<TenantHeaderOperationFilter>();
});

// ============================================
// FileManager-Specific Services
// ============================================
// Configure FileManager Options
builder.Services.Configure<FileManagerOptions>(
    builder.Configuration.GetSection("FileManagerOptions"));

// Register repositories and services
builder.Services.AddScoped<IFileManagerRepository, FileManagerRepository>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IFileManagerService, FileManagerService>();

// ============================================
// Service-to-Service HTTP Clients
// ============================================
// Register Notification service client for service-to-service communication
builder.Services.AddNotificationServiceClient(
    builder.Configuration,
    "FileManagerService",
    builder.Environment.IsDevelopment());

// Register Tenant service client for service-to-service communication (used by background jobs)
builder.Services.AddTenantServiceClient<FileManager.Application.Interfaces.ITenantServiceClient, FileManager.Infrastructure.Services.TenantServiceClient>(
    builder.Configuration,
    "FileManagerService",
    builder.Environment.IsDevelopment());

// ============================================
// Background Jobs
// ============================================
builder.Services.AddHostedService<FileManager.Infrastructure.BackgroundJobs.TempFileCleanupService>();

var app = builder.Build();

// ============================================
// Middleware Pipeline
// ============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Response Compression
app.UseResponseCompression();

// Rate limiting (before authentication)
app.UseRateLimiter();

// ============================================
// Static Files Middleware - Serve uploaded files directly
// ============================================
var fileStoragePath = builder.Configuration["FileManagerOptions:FilesSavePath"] ?? "C:/FileStorage";
if (!Directory.Exists(fileStoragePath))
{
    Directory.CreateDirectory(fileStoragePath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(fileStoragePath),
    RequestPath = "",
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = ctx =>
    {
        // Enable CORS for static files
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        // Cache static files for 1 day
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
    }
});

app.UseCors();

// Localization middleware (must be before exception handler)
app.UseLocalization();

// Global exception handling
app.UseGlobalExceptionHandler();

// Multi-tenancy middleware
app.UseTenantResolution(builder.Configuration);

// JWT tenant verification (AFTER tenant resolution, BEFORE authentication)
app.UseJwtTenantVerification(builder.Configuration);

// Tenant-aware CORS
app.UseTenantAwareCors();

// Multi-tenancy configuration
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");

// Always migrate global database first (ensures it exists for admin endpoints without tenantId)
app.UseDefaultDatabaseMigration<FileManagerDbContext>();

if (multiTenancyEnabled)
{
    // Also enable tenant-aware database migration for tenant-specific requests
    app.UseTenantDatabaseMigration<FileManagerDbContext>(builder.Configuration);
}

// Service authentication middleware (must be BEFORE UseAuthentication)
app.UseServiceAuthentication();

app.UseAuthentication();
app.UseAuthorization();

// ============================================
// Endpoints
// ============================================
app.MapFileManagerEndpoints();

app.MapGet("/", () => new
{
    service = "FileManager API",
    version = "1.0",
    status = "Running",
    timestamp = DateTime.UtcNow
}).WithTags("Health");

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }

// Operation filter to add x-tenant-id header to Swagger
public class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "x-tenant-id",
            In = ParameterLocation.Header,
            Description = "Tenant identifier for multi-tenancy (required when MultiTenancy:Enabled is true)",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string"
            }
        });
    }
}
