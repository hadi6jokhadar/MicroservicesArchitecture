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
using FileManager.Infrastructure.Extensions;
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
// Observability (OpenTelemetry → Jaeger + Prometheus)
// ============================================
builder.Services.AddPlatformObservability(builder.Configuration, "FileManagerService");

// ============================================
// Identity Services
// ============================================
builder.Services.AddScoped<IhsanDev.Shared.Infrastructure.Services.Identity.ICurrentUserService, IhsanDev.Shared.Infrastructure.Services.CurrentUserService>();

// ============================================
// Multi-Tenancy Support (Optional)
// ============================================
builder.Services.AddMultiTenancy(builder.Configuration);

// ============================================
// Database Configuration (Multi-Provider)
// ============================================
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddAuditService();
builder.Services.AddAuditLogQueries<FileManagerDbContext>();

// Add database migration service for automatic database creation
builder.Services.AddDatabaseMigration();

// ============================================
// Authentication & Authorization
// ============================================
builder.Services.AddJwtAuthentication(builder.Configuration);

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
    // Gateway handles Global + PerIP — services apply PerTenant/PerUser only

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



// ============================================
// Health Checks
// ============================================
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration["DatabaseSettings:ConnectionString"]!,
        name: "filemanager-database",
        tags: ["database", "postgresql"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck(
        name: "filemanager-service",
        check: () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("FileManager service is running"),
        tags: ["service"]);

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

app.UseCorrelationId();

// Localization middleware (must be before exception handler)
app.UseLocalization();

// Global exception handling
app.UseGlobalExceptionHandler();

// Migrate global/default DB BEFORE tenant resolution. When multi-tenancy is enabled,
// AddDatabaseContext leaves IsConfigured=false so OnConfiguring uses ITenantContext to
// pick a connection string. If UseDefaultDatabaseMigration runs after UseTenantResolution,
// the first request already has a tenant context, so the static _isMigrated flag fires
// against the tenant DB — leaving the global fallback DB permanently un-migrated.
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");
app.UseDefaultDatabaseMigration<FileManagerDbContext>();

// Multi-tenancy middleware (must be before CORS and authentication)
app.UseTenantResolution(builder.Configuration);

// Tenant-aware CORS (validates origins based on tenant config or appsettings)
// Must be after tenant resolution to access tenant context
// MUST be BEFORE JwtTenantVerification to handle OPTIONS preflight requests first
app.UseTenantAwareCors();

// JWT tenant verification (AFTER tenant resolution and CORS, BEFORE authentication)
// Prevents users from accessing other tenants by changing x-tenant-id header
app.UseJwtTenantVerification(builder.Configuration);

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
app.MapAuditLogEndpoints();

// ============================================
// Health Check Endpoints
// ============================================
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
}).AllowAnonymous();

app.MapHealthChecks("/health/ready").AllowAnonymous();

await app.Services.InitializeDatabaseAsync<FileManagerDbContext>(
    applyMigrations: true,
    seedData: false);

app.MapPrometheusScrapingEndpoint("/metrics");

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
