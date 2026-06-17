using FluentValidation;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Filters;
using IhsanDev.Shared.Infrastructure.Services;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using MediatR;
using Translation.API.Extensions;
using Translation.Application.Commands;
using Translation.Infrastructure.Persistence;
using Translation.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Shared Services (Reusable across all microservices)
// ============================================
// MediatR and FluentValidation
var applicationAssembly = typeof(SetTranslationCommand).Assembly; // Translation.Application assembly

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(applicationAssembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(applicationAssembly);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddPlatformApiVersioning();

// ============================================
// Localization
// ============================================
builder.Services.AddLocalizationService();

// ============================================
// Custom Logging
// ============================================
builder.Services.AddCustomLogging(builder.Configuration, "Translation");

// ============================================
// Observability (OpenTelemetry → Jaeger + Prometheus)
// ============================================
builder.Services.AddPlatformObservability(builder.Configuration, "TranslationService");

// ============================================
// Multi-Tenancy Support (DISABLED)
// ============================================
// Translation Service uses a GLOBAL database (single database for all tenants)
// Multi-tenancy is DISABLED because we don't need tenant validation
// The x-tenant-id header is accepted as an optional parameter to filter translations
// Global translations (TenantId = null) are accessible to all tenants
// Tenant-specific translations (TenantId != null) can be stored alongside global ones
// No tenant validation occurs - the header value is simply used for database filtering

// ============================================
// Database Configuration (GLOBAL DATABASE - NOT MULTI-TENANT)
// ============================================
// Translation Service ALWAYS uses the static connection string from appsettings.json
// It does NOT dynamically connect to different databases based on tenant context
// This service stores translations for all tenants in ONE global database
builder.Services.AddDatabaseContext<TranslationDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(TranslationDbContext).Assembly.GetName().Name);

// Register database migration service
builder.Services.AddDatabaseMigration();

// ============================================
// Authentication & Authorization
// ============================================
// Translation Service uses JWT authentication with shared secret
// Multi-tenancy is disabled, so it ONLY uses the JWT secret from appsettings.json
builder.Services.AddJwtAuthenticationSharedOnly(builder.Configuration);

// ============================================
// CORS Configuration
// ============================================
// CORS uses origins from appsettings.json only (multi-tenancy is disabled)
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
// Health Checks
// ============================================
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration["DatabaseSettings:ConnectionString"]!,
        name: "translation-database",
        tags: ["database", "postgresql"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck(
        name: "translation-service",
        check: () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Translation service is running"),
        tags: ["service"]);

// ============================================
// Distributed Cache (Redis or Memory)
// ============================================
if (builder.Configuration.GetValue<bool>("Redis:Enabled"))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetValue<string>("Redis:ConnectionString");
        options.InstanceName = builder.Configuration.GetValue<string>("Redis:InstanceName") ?? "MicroservicesApp:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

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
// Application Services
// ============================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Translation Service API",
        Version = "v1",
        Description = "Translation Service for managing multi-language translations with optional tenant-specific overrides. Supports global translations and per-tenant customizations."
    });
    
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
    options.OperationFilter<TenantHeaderOperationFilter>();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Infrastructure Services
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddAuditService();
builder.Services.AddAuditLogQueries<TranslationDbContext>();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync<TranslationDbContext>(
    applyMigrations: true,
    seedData: false);

// ============================================
// Middleware Pipeline
// ============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCorrelationId();

// Localization middleware (must be before exception handler)
app.UseLocalization();

app.UseGlobalExceptionHandler();
app.UseResponseCompression(); // Enable response compression for better network performance
app.UseHttpsRedirection();

// CORS middleware (multi-tenancy is disabled, using static CORS from appsettings.json)
app.UseCors();

// Automatic database migration on startup
// Translation Service uses ONLY global database (single database for all tenants)
// Unlike Identity which uses both global and tenant-specific databases
app.UseDefaultDatabaseMigration<TranslationDbContext>();

app.UseAuthentication();
app.UseAuthorization();

// ============================================
// Endpoints
// ============================================
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

app.MapTranslationEndpoints();
app.MapAuditLogEndpoints();

app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();

// Make Program class accessible for testing
public partial class Program { }

// Operation filter to add x-tenant-id header to Swagger
public class TenantHeaderOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        operation.Parameters ??= new List<Microsoft.OpenApi.Models.OpenApiParameter>();

        operation.Parameters.Add(new Microsoft.OpenApi.Models.OpenApiParameter
        {
            Name = "x-tenant-id",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Tenant identifier for multi-tenancy (optional - supports tenant-specific translation overrides)",
            Required = false,
            Schema = new Microsoft.OpenApi.Models.OpenApiSchema
            {
                Type = "string"
            }
        });
    }
}
