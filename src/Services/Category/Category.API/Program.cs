using Category.API.Endpoints;
using Category.Application.Handlers.CreateCategory;
using Category.Infrastructure.Extensions;
using Category.Infrastructure.Persistence;
using FluentValidation;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Middleware;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// MediatR and FluentValidation
// ============================================
var applicationAssembly = typeof(CreateCategoryCommandHandler).Assembly;

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
builder.Services.AddCustomLogging(builder.Configuration, "Category");

// ============================================
// Identity Services
// ============================================
builder.Services.AddScoped<IhsanDev.Shared.Infrastructure.Services.Identity.ICurrentUserService,
    IhsanDev.Shared.Infrastructure.Services.CurrentUserService>();

// ============================================
// Multi-Tenancy Support
// ============================================
builder.Services.AddMultiTenancy(builder.Configuration);

// ============================================
// Infrastructure (Database + Repositories + Redis Cache)
// ============================================
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddDatabaseMigration();

// ============================================
// Authentication & Authorization
// ============================================
builder.Services.AddJwtAuthentication(builder.Configuration);

// ============================================
// CORS
// ============================================
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

    options.AddPolicy("PerTenant", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Request.Headers["x-tenant-id"].FirstOrDefault() ?? "default",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PerTenant:PermitLimit", 2000),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimiting:PerTenant:WindowMinutes", 1)),
                QueueLimit = 50
            }));

    options.AddPolicy("PerUser", context =>
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PerUser:PermitLimit", 500),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimiting:PerUser:WindowMinutes", 1)),
                QueueLimit = 20
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

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
// Response Compression
// ============================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

// ============================================
// Swagger / OpenAPI
// ============================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Category API",
        Version = "v1",
        Description = "Category Service — hierarchical category management with per-tenant database isolation"
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

    options.OperationFilter<TenantHeaderOperationFilter>();
});

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
app.UseResponseCompression();
app.UseRateLimiter();
app.UseCors();
app.UseLocalization();
app.UseGlobalExceptionHandler();

// Migrate global/default DB BEFORE tenant resolution so the DbContext uses the
// default connection string (no tenant context set yet). This ensures the global
// database is migrated on the first request regardless of whether a tenant header
// is present. Moving this AFTER UseTenantResolution causes the static _isMigrated
// flag to be set against the first tenant's DB, leaving the global DB un-migrated.
app.UseDefaultDatabaseMigration<CategoryDbContext>();

// Multi-tenancy (ORDER IS CRITICAL)
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();
app.UseJwtTenantVerification(builder.Configuration);

var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");

if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<CategoryDbContext>(builder.Configuration);
}

app.UseServiceAuthentication();
app.UseAuthentication();
app.UseAuthorization();

// ============================================
// Endpoints
// ============================================
app.MapCategoryEndpoints();
app.MapCategoryInternalEndpoints();

app.MapGet("/", () => new
{
    service = "Category API",
    version = "1.0",
    status = "Running",
    timestamp = DateTime.UtcNow
}).WithTags("Health");

await app.Services.InitializeDatabaseAsync<CategoryDbContext>(
    applyMigrations: true,
    seedData: false);

app.Run();

public partial class Program { }

public class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<Microsoft.OpenApi.Models.OpenApiParameter>();
        operation.Parameters.Add(new Microsoft.OpenApi.Models.OpenApiParameter
        {
            Name = "x-tenant-id",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Required = false,
            Schema = new Microsoft.OpenApi.Models.OpenApiSchema { Type = "string" },
            Description = "Tenant identifier for multi-tenant requests"
        });
    }
}
