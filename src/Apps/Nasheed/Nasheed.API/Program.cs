using FluentValidation;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nasheed.API.Endpoints;
using Nasheed.Application.Handlers.CreateArtist;
using Nasheed.Infrastructure.Extensions;
using Nasheed.Infrastructure.Persistence;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// MediatR + FluentValidation
// ============================================
var applicationAssembly = typeof(CreateArtistCommandHandler).Assembly;

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
builder.Services.AddCustomLogging(builder.Configuration, "Nasheed");

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
// Infrastructure (DB + Repositories + AI Client + Worker)
// ============================================
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddAuditService();
builder.Services.AddAuditLogQueries<NasheedDbContext>();
builder.Services.AddFeatureFlagService();

// ============================================
// Service-to-Service HTTP Clients
// ============================================
builder.Services.AddFileManagerServiceClient(builder.Configuration, "NasheedService", builder.Environment.IsDevelopment());
builder.Services.AddScoped<Nasheed.Application.Helpers.NasheedFileManagerHelper>();

// ============================================
// Automatic DB Migration
// ============================================
builder.Services.AddDatabaseMigration();

// ============================================
// Authentication & Authorization
// ============================================
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    // SuperAdmin (internal service accounts) + Admin + Superadmin (user roles) can access destructive ops.
    // ServiceAuthenticationMiddleware assigns role "SuperAdmin" (capital A) for S2S calls.
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin", "Superadmin", "SuperAdmin"));
});

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
        Title = "Nasheed API",
        Version = "v1",
        Description = "Nasheed Library Service — artists, songs, ingestion pipeline, AI search and generation"
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
// Nasheed uses a per-tenant DB from tenant configuration — no static connection string.
// Only a service-level liveness check is registered; the DB is not probed here.
builder.Services.AddHealthChecks()
    .AddCheck(
        name: "nasheed-service",
        check: () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Nasheed service is running"),
        tags: ["service"]);

// ============================================
// Build
// ============================================
var app = builder.Build();

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
app.UseCors();
app.UseCorrelationId();
app.UseLocalization();
app.UseGlobalExceptionHandler();

// Multi-tenancy (Strategy B)
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();
app.UseJwtTenantVerification(builder.Configuration);

// NOTE: UseDefaultDatabaseMigration is intentionally NOT called here.
// Nasheed has no global database — the DB connection comes from the single tenant's config.
// Migration is run by NasheedTenantLoaderService on startup after the tenant is loaded.
// UseTenantDatabaseMigration handles any subsequent per-tenant migration checks on HTTP requests.
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");
if (multiTenancyEnabled)
    app.UseTenantDatabaseMigration<NasheedDbContext>(builder.Configuration);

// Service-to-service auth (before UseAuthentication)
app.UseServiceAuthentication();

app.UseAuthentication();
app.UseAuthorization();

// ============================================
// Endpoints
// ============================================
app.MapNasheedEndpoints();
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
