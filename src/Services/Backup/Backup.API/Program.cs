using Backup.API.Endpoints;
using Backup.Application.Commands;
using Backup.Infrastructure.Extensions;
using Backup.Infrastructure.Persistence;
using FluentValidation;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Infrastructure.Services;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Shared Services (Reusable across all microservices)
// ============================================
// Scan both this project's assembly (audit-log endpoints etc.) and Backup.Application (commands,
// queries, and their handlers — the handlers themselves live in Backup.Infrastructure to avoid a
// circular Application<->Infrastructure project reference, but MediatR only needs the request
// *types* to be discoverable here; scanning Backup.Infrastructure's assembly too picks up the
// handler implementations).
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(TriggerBackupCommand).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(BackupDbContext).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(TriggerBackupCommand).Assembly);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddPlatformApiVersioning();

// ============================================
// Localization
// ============================================
builder.Services.AddLocalizationService();

// ============================================
// Custom Logging
// ============================================
builder.Services.AddCustomLogging(builder.Configuration, "Backup");

// ============================================
// Observability (OpenTelemetry → Jaeger + Prometheus)
// ============================================
builder.Services.AddPlatformObservability(builder.Configuration, "BackupService");

// ============================================
// Database Configuration (Strategy A — Single Global DB, NOT multi-tenant)
// ============================================
// Backup Service ALWAYS uses the static connection string from appsettings.json. It stores its
// own operational metadata (backup targets, backup run history, restore run history) and does
// NOT participate in multi-tenancy itself — see BackupDbContext and
// .claude/instructions/database-strategy.instructions.md (Strategy A).
builder.Services.AddInfrastructureServices(builder.Configuration, isDevelopment: builder.Environment.IsDevelopment());

// Register database migration service (required for UseDefaultDatabaseMigration middleware)
builder.Services.AddDatabaseMigration();

// ============================================
// Hangfire (own Postgres storage — no Redis dependency, so registered unconditionally)
// ============================================
builder.Services.AddBackupHangfire(builder.Configuration);

// ============================================
// Authentication & Authorization
// ============================================
// Backup Service ALWAYS uses JWT settings from appsettings.json — no per-tenant JWT resolution.
builder.Services.AddJwtAuthenticationSharedOnly(builder.Configuration);

// ============================================
// CORS Configuration
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
    options.SwaggerDoc("v1", new() { Title = "Backup Service API", Version = "v1" });

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
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ============================================
// Audit Logging
// ============================================
builder.Services.AddAuditService();
builder.Services.AddAuditLogQueries<BackupDbContext>();

// ============================================
// Health Checks
// ============================================
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration["DatabaseSettings:ConnectionString"]!,
        name: "backup-database",
        tags: ["database", "postgresql"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck(
        name: "backup-service",
        check: () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Backup service is running"),
        tags: ["service"]);

// ============================================
// Build & Configure Pipeline
// ============================================
var app = builder.Build();

await app.Services.InitializeDatabaseAsync<BackupDbContext>(
    applyMigrations: true,
    seedData: false);

// Seed backup targets once at startup so the admin UI never shows an empty Overview table on a
// fresh deployment — otherwise targets only appear after the first nightly BackupSchedulerJob run
// (01:00 UTC) or after an admin manually triggers a backup for that service/tenant by name.
await using (var startupScope = app.Services.CreateAsyncScope())
{
    var startupLogger = startupScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var globalTargetSyncJob = startupScope.ServiceProvider.GetRequiredService<Backup.Infrastructure.Jobs.GlobalTargetSyncJob>();
        await globalTargetSyncJob.SyncAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Startup global backup target sync failed — will retry on the next scheduled run.");
    }

    try
    {
        var tenantTargetSyncJob = startupScope.ServiceProvider.GetRequiredService<Backup.Infrastructure.Jobs.TenantTargetSyncJob>();
        await tenantTargetSyncJob.SyncAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        // Tenant Service may not be reachable yet at Backup's own startup (e.g. both starting
        // together) — this is best-effort; the nightly BackupSchedulerJob retries it regardless.
        startupLogger.LogWarning(ex, "Startup tenant backup target sync failed — will retry on the next scheduled run.");
    }
}

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
app.UseCors();

// Automatic database migration for the (only) global database. Strategy A has no per-tenant
// migration step — see .claude/instructions/database-strategy.instructions.md.
app.UseDefaultDatabaseMigration<BackupDbContext>();

// Service authentication middleware (must be BEFORE UseAuthentication)
// Allows service-to-service communication with shared secret
app.UseServiceAuthentication();

app.UseAuthentication();
app.UseAuthorization();

// ============================================
// Map API Endpoints
// ============================================
app.MapBackupEndpoints();
app.MapAuditLogEndpoints();

// ============================================
// Hangfire Dashboard + Recurring Jobs
// ============================================
app.UseBackupHangfireDashboard(builder.Configuration);
HangfireExtensions.RegisterBackupRecurringJobs();

app.MapPrometheusScrapingEndpoint("/metrics");

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

// Make the implicit Program class public for testing
public partial class Program { }
