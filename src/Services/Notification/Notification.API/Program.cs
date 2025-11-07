using System.Text;
using FluentValidation;
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

    // Support SignalR token from query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            // If the request is for SignalR hub
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
            {
                context.Token = accessToken;
            }

            // Support per-tenant JWT validation when JwtMode is PerTenant
            if (jwtMode == JwtMode.PerTenant)
            {
                var tenantContext = context.HttpContext.RequestServices.GetService<ITenantContext>();
                if (tenantContext?.HasTenant == true && tenantContext.CurrentTenant?.Configuration?.Jwt != null)
                {
                    var tenantJwt = tenantContext.CurrentTenant.Configuration.Jwt;
                    if (!string.IsNullOrEmpty(tenantJwt.Secret))
                    {
                        context.Options.TokenValidationParameters.IssuerSigningKey =
                            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tenantJwt.Secret));
                        context.Options.TokenValidationParameters.ValidIssuer = tenantJwt.Issuer;
                        context.Options.TokenValidationParameters.ValidAudience = tenantJwt.Audience;
                    }
                }
            }

            return Task.CompletedTask;
        }
        // When JwtMode is Shared, use the JWT settings from appsettings.json
        // All tenants validate tokens using the same JWT secret from Jwt section
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
// SignalR Configuration
// ============================================
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Configuration.GetValue<bool>("SignalR:EnableDetailedErrors", false);
    options.ClientTimeoutInterval = TimeSpan.Parse(
        builder.Configuration["SignalR:ClientTimeoutInterval"] ?? "00:01:00");
    options.KeepAliveInterval = TimeSpan.Parse(
        builder.Configuration["SignalR:KeepAliveInterval"] ?? "00:00:15");
});

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
// Services
// ============================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register infrastructure services (repositories, services, etc.)
builder.Services.AddInfrastructureServices();

// HTTP Client for Identity Service (for device tokens)
builder.Services.AddHttpClient("IdentityService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["IdentityService:BaseUrl"] ?? "https://localhost:5001");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

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

// AutoMapper - Using specific assemblies to ensure all mappings are found
builder.Services.AddAutoMapper(
    applicationAssembly,  // Notification.Application
    typeof(IhsanDev.Shared.Application.Common.Mappings.MappingProfile).Assembly  // Shared.Application
);

// ============================================
// Build & Configure Pipeline
// ============================================
var app = builder.Build();

// ============================================
// Log Startup Configuration
// ============================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();
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

app.UseGlobalExceptionHandler();
app.UseResponseCompression(); // Enable response compression for better network performance
app.UseHttpsRedirection();

// Multi-tenancy middleware (must be before CORS and authentication)
// Only runs if MultiTenancy:Enabled is true
app.UseTenantResolution(builder.Configuration);

// Tenant-aware CORS (validates origins based on tenant config or appsettings)
// Must be after tenant resolution to access tenant context
// This middleware handles both preflight (OPTIONS) and actual requests
app.UseTenantAwareCors();

// Note: Standard UseCors() is NOT needed because TenantAwareCors handles everything

// Automatic database migration - use EITHER tenant or default based on configuration
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false);
if (multiTenancyEnabled)
{
    // Multi-tenancy enabled: Use tenant database migration
    // This ensures tenant databases are created and migrated automatically
    app.UseTenantDatabaseMigration<NotificationDbContext>(builder.Configuration);
    
    // Also handle tenant-specific notification database
    app.UseTenantDatabaseMigration<TenantNotificationDbContext>(builder.Configuration);
}
else
{
    // Multi-tenancy disabled: Use default database migration
    // This ensures the default database from appsettings.json is created and migrated
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
app.MapHub<NotificationHub>("/hubs/notifications");

// ============================================
// Health Check
// ============================================
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "notification" }))
    .WithName("HealthCheck")
    .AllowAnonymous();

// ============================================
// Start Application
// ============================================
logger.LogInformation("========================================");
logger.LogInformation("Notification API Started Successfully!");
logger.LogInformation("========================================");

app.Run();

// Make Program class accessible to tests
public partial class Program { }
