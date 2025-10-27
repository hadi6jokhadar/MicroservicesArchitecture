using System.Text;
using FluentValidation;
using Tenant.Application.Commands.Tenant;
using Tenant.Infrastructure.Extensions;
using Tenant.Infrastructure.Persistence;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Filters;
using IhsanDev.Shared.Infrastructure.Services;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Enums;
using Tenant.API.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Shared Services (Reusable across all microservices)
// ============================================
// MediatR and FluentValidation
var applicationAssembly = typeof(CreateTenantCommand).Assembly; // Tenant.Application assembly

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
builder.Services.AddCustomLogging(builder.Configuration, "Tenant");

// ============================================
// Database Configuration (Multi-Provider)
// ============================================
builder.Services.AddDatabaseContext<TenantDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(TenantDbContext).Assembly.GetName().Name);

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
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ============================================
// Application Services
// ============================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Tenant Service API", Version = "v1" });
    
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

// AutoMapper - Using specific assemblies
builder.Services.AddAutoMapper(
    applicationAssembly,  // Tenant.Application
    typeof(IhsanDev.Shared.Application.Common.Mappings.MappingProfile).Assembly  // Shared.Application
);

// Infrastructure Services
builder.Services.AddInfrastructureServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ============================================
// Build & Configure Pipeline
// ============================================
var app = builder.Build();

// Initialize database (Development only)
if (app.Environment.IsDevelopment())
{
    await app.Services.InitializeDatabaseAsync<TenantDbContext>(
        applyMigrations: true,
        seedData: false); // No seed data for tenant service
    
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ============================================
// Map API Endpoints (Grouped Minimal APIs)
// ============================================
app.MapTenantEndpoints();

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
