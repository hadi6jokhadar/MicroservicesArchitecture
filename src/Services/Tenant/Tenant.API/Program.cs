using System.Text;
using FluentValidation;
using Tenant.Application.Commands.Tenant;
using Tenant.Infrastructure.Extensions;
using Tenant.Infrastructure.Persistence;
using IhsanDev.Shared.Application.Common.Behaviors;
using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Services;
using IhsanDev.Shared.Infrastructure.Services.Identity;
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
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Key"] ?? jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JWT Key/Secret is not configured");

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
