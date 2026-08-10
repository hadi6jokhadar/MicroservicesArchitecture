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
var infrastructureAssembly = typeof(NasheedDbContext).Assembly;

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(applicationAssembly);
    // UpdateSongCommandHandler lives in .Infrastructure (needs ICurrentUserService for the
    // ownership check) — see Dotnet.instructions.md pitfall #14.
    cfg.RegisterServicesFromAssembly(infrastructureAssembly);
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
builder.Services.AddNotificationServiceClient(builder.Configuration, "NasheedService", builder.Environment.IsDevelopment());

// ============================================
// Automatic DB Migration
// ============================================
builder.Services.AddDatabaseMigration();

// Eagerly migrate + seed a newly created tenant's database the moment Tenant Service
// broadcasts it — removes the need to restart this service to trigger migration.
// No-op when multi-tenancy or Redis is disabled (see AUTOMATIC_DATABASE_MIGRATION.md).
builder.Services.AddTenantProvisioningListener<NasheedDbContext>(builder.Configuration);

// Refresh INasheedTenantCache the moment Tenant Service broadcasts a config/feature-flag change —
// removes the need to restart this service to pick up a flag toggle. No-op when Redis is disabled
// (the periodic fallback in NasheedTenantLoaderService still applies either way).
builder.Services.AddNasheedTenantConfigUpdatedListener(builder.Configuration);

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

    // Content-editor policies: Admin/SuperAdmin always pass (same roles as AdminOnly), OR a
    // lower-privileged user (e.g. a "NasheedDataEntry" role created via the Identity admin UI)
    // holding the matching "Permission" claim. These are additive, narrower alternatives to
    // AdminOnly for create/edit — delete stays AdminOnly-only, see NasheedEndpoints.cs.
    // The claim itself is plain data (ClaimType="Permission", ClaimValue="nasheed.songs.create"
    // etc.) created and assigned through Identity's existing Roles/Claims admin UI — no seeding
    // required. See Doc/SHARED_IDENTITY_SERVICE_GUIDE.md "Permission Claims" section.
    static bool IsAdmin(Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext ctx) =>
        ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Superadmin") || ctx.User.IsInRole("SuperAdmin");

    options.AddPolicy("SongsCreate", policy => policy.RequireAssertion(ctx =>
        IsAdmin(ctx) || ctx.User.HasClaim("Permission", "nasheed.songs.create")));
    options.AddPolicy("SongsEdit", policy => policy.RequireAssertion(ctx =>
        IsAdmin(ctx) || ctx.User.HasClaim("Permission", "nasheed.songs.edit")));
    options.AddPolicy("ArtistsCreate", policy => policy.RequireAssertion(ctx =>
        IsAdmin(ctx) || ctx.User.HasClaim("Permission", "nasheed.artists.create")));
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
// Exception handler must be the FIRST middleware so it wraps everything downstream —
// including correlation-ID/localization, HTTPS-redirect, and compression — not just what
// happens to be registered after it. Earlier middleware catches exceptions from later
// middleware (ASP.NET Core wraps each Use() call's next() inside the previous one's
// try/catch), so anything registered before this line would bypass it entirely.
// See Dotnet.instructions.md.
app.UseGlobalExceptionHandler();
app.UseCorrelationId();
app.UseLocalization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
// Note: Standard UseCors() is NOT needed/used because TenantAwareCors (below) handles everything.
// DO NOT call app.UseCors() here - it conflicts with TenantAwareCorsMiddleware.

// Multi-tenancy (Strategy B)
app.UseTenantResolution(builder.Configuration);
app.UseTenantAwareCors();

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

// JWT tenant verification — MUST be AFTER UseAuthentication(): it reads context.User,
// which UseAuthentication() populates. Prevents users from accessing other tenants by
// changing the x-tenant-id header.
app.UseJwtTenantVerification(builder.Configuration);

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
