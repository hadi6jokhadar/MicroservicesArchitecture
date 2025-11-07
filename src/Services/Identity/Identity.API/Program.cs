// Program.cs
using System.Text;
using FluentValidation;
using Identity.Application.Commands;
using Identity.Infrastructure.Extensions;
using Identity.Infrastructure.Persistence;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Filters;
using IhsanDev.Shared.Infrastructure.Middleware;
using IhsanDev.Shared.Infrastructure.Services;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Enums;
using Identity.API.Extensions;
using Identity.API.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Shared Services (Reusable across all microservices)
// ============================================
// MediatR and FluentValidation (without AutoMapper from shared extension)
var applicationAssembly = typeof(RegisterCommand).Assembly; // Identity.Application assembly

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(applicationAssembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(applicationAssembly);
builder.Services.AddGlobalExceptionHandler();

// ============================================
// Custom Logging
// ============================================
builder.Services.AddCustomLogging(builder.Configuration, "Identity");

// ============================================
// Multi-Tenancy Support (Optional)
// ============================================
builder.Services.AddMultiTenancy(builder.Configuration);

// ============================================
// Database Configuration (Multi-Provider)
// ============================================
builder.Services.AddDatabaseContext<IdentityDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(IdentityDbContext).Assembly.GetName().Name);

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
                // Resolve tenant-specific JWT settings if available
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
// Note: AddControllers() removed since we're using Minimal APIs
// Only add it back if you have controllers that haven't been converted yet
// builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Identity Service API", Version = "v1" });
    
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

// AutoMapper - Using specific assemblies to ensure all mappings are found
builder.Services.AddAutoMapper(
    applicationAssembly,  // Identity.Application
    typeof(IhsanDev.Shared.Application.Common.Mappings.MappingProfile).Assembly  // Shared.Application
);

// Infrastructure Services
builder.Services.AddInfrastructureServices();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register validation filters
builder.Services.AddScoped(typeof(ValidationFilter<>));

// ============================================
// HTTP Clients for Service-to-Service Communication
// ============================================
// HTTP Client for Notification Service
builder.Services.AddHttpClient("NotificationService", client =>
{
    var baseUrl = builder.Configuration["Services:NotificationService:BaseUrl"] 
        ?? "https://localhost:5104";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    
    var timeout = builder.Configuration.GetValue<int>("Services:NotificationService:Timeout", 30);
    client.Timeout = TimeSpan.FromSeconds(timeout);
    
    // Add service authentication headers
    var serviceSecret = builder.Configuration["ServiceCommunication:SharedSecret"];
    if (!string.IsNullOrEmpty(serviceSecret))
    {
        client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
        client.DefaultRequestHeaders.Add("X-Service-Name", "IdentityService");
    }
});

// ============================================
// Build & Configure Pipeline
// ============================================
var app = builder.Build();

// ============================================
// Log Startup Configuration
// ============================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("========================================");
logger.LogInformation("Identity API Starting...");
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
    await app.Services.InitializeDatabaseAsync<IdentityDbContext>(
        applyMigrations: true,
        seedData: true);
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
    app.UseTenantDatabaseMigration<IdentityDbContext>(builder.Configuration);
}
else
{
    // Multi-tenancy disabled: Use default database migration
    // This ensures the default database from appsettings.json is created and migrated
    app.UseDefaultDatabaseMigration<IdentityDbContext>();
}

// Service authentication middleware (must be BEFORE UseAuthentication)
// Allows service-to-service communication with shared secret
app.UseServiceAuthentication();

app.UseAuthentication();
app.UseAuthorization();

// ============================================
// Map API Endpoints (Grouped Minimal APIs)
// ============================================

// Map user-related endpoints (profile management)
app.MapUserEndpoints();

// Map admin-related endpoints (user management)
app.MapAdminEndpoints();

// Map auth-related endpoints (authentication)
app.MapAuthEndpoints();

// Keep controllers if you still have other controllers that haven't been converted
// app.MapControllers();

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }