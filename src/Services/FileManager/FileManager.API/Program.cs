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
var isDevEnvironment = builder.Environment.IsDevelopment();
builder.Services.AddHttpClient("NotificationService", client =>
{
    var baseUrl = builder.Configuration["Services:NotificationService:BaseUrl"]
        ?? "https://localhost:5104";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    var serviceSecret = builder.Configuration["ServiceCommunication:SharedSecret"];
    if (!string.IsNullOrEmpty(serviceSecret))
    {
        client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
        client.DefaultRequestHeaders.Add("X-Service-Name", "FileManagerService");
    }
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (isDevEnvironment)
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// Typed HttpClient for Tenant Service (used by background jobs)
var isDevelopment = builder.Environment.IsDevelopment();
builder.Services.AddHttpClient<FileManager.Application.Interfaces.ITenantServiceClient, FileManager.Infrastructure.Services.TenantServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:TenantService:BaseUrl"]
        ?? builder.Configuration["MultiTenancy:TenantServiceUrl"]
        ?? "https://localhost:5002";
    
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    var serviceSecret = builder.Configuration["ServiceCommunication:SharedSecret"];
    if (!string.IsNullOrEmpty(serviceSecret))
    {
        client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
        client.DefaultRequestHeaders.Add("X-Service-Name", "FileManagerService");
    }
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (isDevelopment)
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

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

// Global exception handling
app.UseGlobalExceptionHandler();

// Multi-tenancy middleware
app.UseTenantResolution(builder.Configuration);

// Tenant-aware CORS
app.UseTenantAwareCors();

// Multi-tenancy configuration
var multiTenancyEnabled = builder.Configuration.GetValue<bool>("MultiTenancy:Enabled");

if (multiTenancyEnabled)
{
    // Tenant-aware database migration
    app.UseTenantDatabaseMigration<FileManagerDbContext>(builder.Configuration);
}
else
{
    // Default database migration (single-tenant mode)
    app.UseDefaultDatabaseMigration<FileManagerDbContext>();
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
