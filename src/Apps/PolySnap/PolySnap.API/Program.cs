using FluentValidation;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Middleware;
using Microsoft.OpenApi.Models;
using PolySnap.API.Endpoints;
using PolySnap.Application.Handlers.CreateSnapRequest;
using PolySnap.Infrastructure.Extensions;
using PolySnap.Infrastructure.Persistence;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// MediatR + FluentValidation
// ============================================
var applicationAssembly = typeof(CreateSnapRequestCommandHandler).Assembly;

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
builder.Services.AddCustomLogging(builder.Configuration, "PolySnap");

// ============================================
// Identity Services
// ============================================
builder.Services.AddScoped<IhsanDev.Shared.Infrastructure.Services.Identity.ICurrentUserService,
    IhsanDev.Shared.Infrastructure.Services.CurrentUserService>();

// ============================================
// Multi-Tenancy
// ============================================
builder.Services.AddMultiTenancy(builder.Configuration);

// ============================================
// Infrastructure (Database + Repositories)
// ============================================
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddAuditService();
builder.Services.AddAuditLogQueries<PolySnapDbContext>();
builder.Services.AddFeatureFlagService();
builder.Services.AddDatabaseMigration();

// Eagerly migrate + seed a newly created tenant's database the moment Tenant Service
// broadcasts it — removes the need to restart this service to trigger migration.
// No-op when multi-tenancy or Redis is disabled (see AUTOMATIC_DATABASE_MIGRATION.md).
builder.Services.AddTenantProvisioningListener<PolySnapDbContext>(builder.Configuration);

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
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
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
// Swagger
// ============================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PolySnap API",
        Version = "v1",
        Description = "PolySnap Service — automated spatial boundary engine (CRUD scaffold; PostGIS/OSM snapping logic added in a later phase)"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: \"Bearer {token}\"",
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
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    options.OperationFilter<TenantHeaderOperationFilter>();
});

// ============================================
// Health Checks
// ============================================
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration["DatabaseSettings:ConnectionString"]!,
        name: "polysnap-database",
        tags: ["database", "postgresql"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck(
        name: "polysnap-service",
        check: () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("PolySnap service is running"),
        tags: ["service"]);

// ============================================
// Build
// ============================================
var app = builder.Build();

await app.Services.InitializeDatabaseAsync<PolySnapDbContext>(
    applyMigrations: true,
    seedData: false);

// Warm the tenant-config cache and eagerly run each tenant's migration check at startup
// instead of paying that cost lazily on the tenant's first real request. No-ops if
// multi-tenancy is disabled (returns an empty tenant list).
var warmedTenants = await app.Services.WarmTenantConfigCacheAsync();
await app.Services.WarmTenantDatabaseMigrationsAsync<PolySnapDbContext>(warmedTenants);

// ============================================
// Middleware Pipeline (ORDER IS CRITICAL)
// ============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
// Note: Standard UseCors() is NOT needed/used because TenantAwareCors (below) handles everything.
// DO NOT call app.UseCors() here - it conflicts with TenantAwareCorsMiddleware.
app.UseCorrelationId();
app.UseLocalization();
app.UseGlobalExceptionHandler();

// Migrate global/default DB BEFORE tenant resolution so the DbContext uses the
// default connection string (no tenant context set yet). This ensures the global
// database is migrated on the first request regardless of whether a tenant header
// is present. Moving this AFTER UseTenantResolution causes the static _isMigrated
// flag to be set against the first tenant's DB, leaving the global DB un-migrated.
app.UseDefaultDatabaseMigration<PolySnapDbContext>();

// Multi-tenancy (ORDER IS CRITICAL)
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();

var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<PolySnapDbContext>(builder.Configuration);
}

// Service-to-service auth (before UseAuthentication)
app.UseServiceAuthentication();

app.UseAuthentication();

// JWT tenant verification — MUST be AFTER UseAuthentication(): it reads context.User,
// which UseAuthentication() populates. Prevents users from accessing other tenants by
// changing the x-tenant-id header.
app.UseJwtTenantVerification(builder.Configuration);

app.UseAuthorization();

// ============================================
// Endpoints
// ============================================
app.MapPolySnapEndpoints();
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

app.Run();

public partial class Program { }

public class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "x-tenant-id",
            In = ParameterLocation.Header,
            Description = "Tenant identifier for multi-tenancy",
            Required = false,
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
