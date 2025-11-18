# New Service Essential Guide

**Quick Reference for Common Service Development Tasks**

> **Prerequisites**: Read `NEW_SERVICE_INTEGRATION_GUIDE.md` for complete service creation steps.

---

## Table of Contents

1. [Tenant Support Configuration](#1-tenant-support-configuration)
2. [Getting Tenant Configuration](#2-getting-tenant-configuration)
3. [Standalone Service (No Tenant)](#3-standalone-service-no-tenant)
4. [Dual Mode Support (Tenant + Normal)](#4-dual-mode-support-tenant--normal)
5. [Tenant vs Global User Endpoints](#5-tenant-vs-global-user-endpoints)
6. [Service-to-Service Communication](#6-service-to-service-communication)
7. [Internal Service-Only Endpoints](#7-internal-service-only-endpoints)
8. [Cache Usage](#8-cache-usage)
9. [Multiple DbContexts in Same Service](#9-multiple-dbcontexts-in-same-service)
10. [Localization](#10-localization)

---

## 1. Tenant Support Configuration

### Step 1: Add Required Packages

```xml
<!-- In {Service}.API.csproj -->
<PackageReference Include="IhsanDev.Shared.Infrastructure" />
<PackageReference Include="IhsanDev.Shared.Kernel" />
```

### Step 2: Configure appsettings.json

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "Shared",
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 30
  }
}
```

### Step 3: Register Services in Program.cs

```csharp
using IhsanDev.Shared.Infrastructure.MultiTenancy;

var builder = WebApplication.CreateBuilder(args);

// Register tenant services
builder.Services.AddTenantServices(builder.Configuration);

// Register database with tenant support
builder.Services.AddDatabaseContext<YourDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(YourDbContext).Assembly.GetName().Name);

var app = builder.Build();

// Add tenant middleware BEFORE authentication
app.UseTenantMiddleware();
app.UseAuthentication();
app.UseAuthorization();

// Add database migration middleware
app.UseTenantDatabaseMigration();
```

### Step 4: Implement ITenantContext in DbContext

```csharp
using IhsanDev.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

public class YourDbContext : DbContext, ITenantContext
{
    private readonly ITenantConfigurationProvider _tenantConfig;

    public YourDbContext(
        DbContextOptions<YourDbContext> options,
        ITenantConfigurationProvider tenantConfig) : base(options)
    {
        _tenantConfig = tenantConfig;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = _tenantConfig.GetConnectionString();
            optionsBuilder.UseNpgsql(connectionString); // Or UseSqlServer, UseMySql, etc.
        }
    }
}
```

---

## 2. Getting Tenant Configuration

### Method 1: Inject ITenantConfigurationProvider

```csharp
public class MyCommandHandler : IRequestHandler<MyCommand, MyResponse>
{
    private readonly ITenantConfigurationProvider _tenantConfig;

    public MyCommandHandler(ITenantConfigurationProvider tenantConfig)
    {
        _tenantConfig = tenantConfig;
    }

    public async Task<MyResponse> Handle(MyCommand request, CancellationToken ct)
    {
        var tenantId = _tenantConfig.GetTenantId();
        var connectionString = _tenantConfig.GetConnectionString();
        var customConfig = _tenantConfig.GetConfigurationValue("CustomKey");

        // Your logic here
    }
}
```

### Method 2: Access from HttpContext

```csharp
app.MapGet("/api/tenant-info", (HttpContext context) =>
{
    var tenantId = context.Items["TenantId"]?.ToString();
    var tenantName = context.Items["TenantName"]?.ToString();

    return Results.Ok(new { tenantId, tenantName });
}).RequireAuthorization();
```

### Available Methods

```csharp
string tenantId = _tenantConfig.GetTenantId();
string connectionString = _tenantConfig.GetConnectionString();
string value = _tenantConfig.GetConfigurationValue("key");
Dictionary<string, string> allConfigs = _tenantConfig.GetAllConfigurations();
```

---

## 3. Standalone Service (No Tenant)

### Configure appsettings.json

```json
{
  "MultiTenancy": {
    "Enabled": false
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=MyServiceDb;Username=user;Password=pass"
  }
}
```

### Program.cs (Simple Setup)

```csharp
var builder = WebApplication.CreateBuilder(args);

// NO tenant services registration
// Just register database normally
builder.Services.AddDatabaseContext<YourDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(YourDbContext).Assembly.GetName().Name);

var app = builder.Build();

// NO tenant middleware
app.UseAuthentication();
app.UseAuthorization();

// Use default migration (no tenant header required)
app.UseDefaultDatabaseMigration();
```

### DbContext (Simple Implementation)

```csharp
public class YourDbContext : DbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options) : base(options)
    {
    }

    // No ITenantContext implementation needed
}
```

---

## 4. Dual Mode Support (Tenant + Normal)

### Configure appsettings.json (Tenant Mode Controllable)

```json
{
  "MultiTenancy": {
    "Enabled": true // Change to false to switch to normal mode
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=DefaultDb;Username=user;Password=pass"
  }
}
```

### Program.cs (Conditional Registration)

```csharp
var builder = WebApplication.CreateBuilder(args);

var multiTenancyEnabled = builder.Configuration
    .GetValue<bool>("MultiTenancy:Enabled");

if (multiTenancyEnabled)
{
    builder.Services.AddTenantServices(builder.Configuration);
}

builder.Services.AddDatabaseContext<YourDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(YourDbContext).Assembly.GetName().Name);

var app = builder.Build();

if (multiTenancyEnabled)
{
    app.UseTenantMiddleware();
}

app.UseAuthentication();
app.UseAuthorization();

if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration();
}
else
{
    app.UseDefaultDatabaseMigration();
}
```

### DbContext (Dual Mode Implementation)

```csharp
public class YourDbContext : DbContext, ITenantContext
{
    private readonly ITenantConfigurationProvider? _tenantConfig;
    private readonly IConfiguration _configuration;

    public YourDbContext(
        DbContextOptions<YourDbContext> options,
        IConfiguration configuration,
        ITenantConfigurationProvider? tenantConfig = null) : base(options)
    {
        _configuration = configuration;
        _tenantConfig = tenantConfig;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var multiTenancyEnabled = _configuration
                .GetValue<bool>("MultiTenancy:Enabled");

            string connectionString;
            if (multiTenancyEnabled && _tenantConfig != null)
            {
                connectionString = _tenantConfig.GetConnectionString();
            }
            else
            {
                connectionString = _configuration
                    .GetConnectionString("DefaultConnection")!;
            }

            optionsBuilder.UseNpgsql(connectionString);
        }
    }
}
```

---

## 5. Tenant vs Global User Endpoints

### Tenant User Endpoint (Requires x-tenant-id Header)

```csharp
// Requires authentication + x-tenant-id header
app.MapGet("/api/tenant/users", async (
    ITenantConfigurationProvider tenantConfig,
    YourDbContext dbContext) =>
{
    var tenantId = tenantConfig.GetTenantId();

    // Access tenant-specific data
    var users = await dbContext.Users
        .Where(u => u.TenantId == tenantId)
        .ToListAsync();

    return Results.Ok(users);
}).RequireAuthorization();
```

### Global User Endpoint (Superadmin Only)

```csharp
// Requires "Superadmin" role - can access ALL tenants
app.MapGet("/api/global/tenants", async (
    HttpClient httpClient,
    ClaimsPrincipal user) =>
{
    if (!user.IsInRole("Superadmin"))
    {
        return Results.Forbid();
    }

    // Call Tenant Service to get all tenants
    var response = await httpClient.GetAsync("https://localhost:5002/api/tenants");
    var tenants = await response.Content.ReadFromJsonAsync<List<TenantDto>>();

    return Results.Ok(tenants);
}).RequireAuthorization();
```

### Mixed Endpoint (Role-Based Logic)

```csharp
app.MapGet("/api/data", async (
    ClaimsPrincipal user,
    ITenantConfigurationProvider? tenantConfig,
    YourDbContext dbContext) =>
{
    if (user.IsInRole("Superadmin"))
    {
        // Global access - return all data
        var allData = await dbContext.Data.ToListAsync();
        return Results.Ok(allData);
    }
    else
    {
        // Tenant access - return filtered data
        var tenantId = tenantConfig?.GetTenantId();
        var tenantData = await dbContext.Data
            .Where(d => d.TenantId == tenantId)
            .ToListAsync();
        return Results.Ok(tenantData);
    }
}).RequireAuthorization();
```

---

## 6. Service-to-Service Communication

### Step 1: Configure appsettings.json

```json
{
  "ServiceAuthentication": {
    "SharedSecret": "your-shared-secret-minimum-32-chars",
    "ServiceName": "YourService"
  },
  "NotificationService": {
    "BaseUrl": "https://localhost:5004"
  }
}
```

### Step 2: Register HTTP Client

```csharp
// In Program.cs
builder.Services.AddHttpClient<INotificationServiceClient, NotificationServiceClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        client.BaseAddress = new Uri(config["NotificationService:BaseUrl"]!);

        // Add service authentication header
        var sharedSecret = config["ServiceAuthentication:SharedSecret"]!;
        client.DefaultRequestHeaders.Add("X-Service-Secret", sharedSecret);
    });
```

### Step 3: Call Another Service

```csharp
public class MyCommandHandler : IRequestHandler<MyCommand, MyResponse>
{
    private readonly INotificationServiceClient _notificationClient;

    public MyCommandHandler(INotificationServiceClient notificationClient)
    {
        _notificationClient = notificationClient;
    }

    public async Task<MyResponse> Handle(MyCommand request, CancellationToken ct)
    {
        // Send notification to another service
        await _notificationClient.SendNotificationAsync(
            tenantId: "acme-corp",
            userId: 123,
            title: "Action Completed",
            message: "Your request was processed successfully");

        return new MyResponse { Success = true };
    }
}
```

### Step 4: Custom Service Client

```csharp
public interface IMyServiceClient
{
    Task<MyData> GetDataAsync(string tenantId, int id);
}

public class MyServiceClient : IMyServiceClient
{
    private readonly HttpClient _httpClient;

    public MyServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MyData> GetDataAsync(string tenantId, int id)
    {
        _httpClient.DefaultRequestHeaders.Add("x-tenant-id", tenantId);

        var response = await _httpClient.GetAsync($"/api/data/{id}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MyData>();
    }
}
```

---

## 7. Internal Service-Only Endpoints

### Step 1: Register Service Authentication Middleware

```csharp
// In Program.cs
using IhsanDev.Shared.Authentication.ServiceAuth;

var builder = WebApplication.CreateBuilder(args);

// Add service authentication
builder.Services.AddServiceAuthentication(builder.Configuration);

var app = builder.Build();

// Add middleware BEFORE endpoints
app.UseServiceAuthenticationMiddleware();
app.UseAuthentication();
app.UseAuthorization();
```

### Step 2: Configure appsettings.json

```json
{
  "ServiceAuthentication": {
    "SharedSecret": "your-shared-secret-minimum-32-chars",
    "ServiceName": "YourService"
  }
}
```

### Step 3: Create Service-Only Endpoint

```csharp
// Endpoint accessible ONLY by services (not public users)
app.MapPost("/api/internal/process", async (
    ProcessRequest request,
    ClaimsPrincipal user,
    YourDbContext dbContext) =>
{
    // Check if caller is a service
    if (!user.IsInRole("Service"))
    {
        return Results.Forbid();
    }

    // Process internal logic
    var result = await dbContext.Data
        .Where(d => d.Id == request.Id)
        .FirstOrDefaultAsync();

    return Results.Ok(result);
}).RequireAuthorization(); // Still requires authorization, but accepts service identity
```

### Step 4: Create Separate Endpoint Groups (Optional)

```csharp
// Public endpoints
var publicApi = app.MapGroup("/api/public");
publicApi.MapGet("/data", async (YourDbContext dbContext) =>
{
    var data = await dbContext.Data.ToListAsync();
    return Results.Ok(data);
}).RequireAuthorization();

// Internal endpoints (service-only)
var internalApi = app.MapGroup("/api/internal")
    .RequireAuthorization()
    .AddEndpointFilter(async (context, next) =>
    {
        var user = context.HttpContext.User;
        if (!user.IsInRole("Service"))
        {
            return Results.Forbid();
        }
        return await next(context);
    });

internalApi.MapPost("/process", async (ProcessRequest request) =>
{
    // Only services can call this
    return Results.Ok();
});
```

---

## 8. Cache Usage

### Step 1: Configure appsettings.json

```json
{
  "Redis": {
    "Enabled": true,
    "Configuration": "localhost:6379",
    "InstanceName": "YourService:"
  }
}
```

### Step 2: Register Cache in Program.cs

```csharp
using IhsanDev.Shared.Infrastructure.Caching;

var builder = WebApplication.CreateBuilder(args);

// Register Redis cache (automatic fallback to in-memory if disabled)
builder.Services.AddRedisCache(builder.Configuration);
```

### Step 3: Use IDistributedCache

```csharp
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

public class MyCommandHandler : IRequestHandler<MyCommand, MyResponse>
{
    private readonly IDistributedCache _cache;
    private readonly YourDbContext _dbContext;

    public MyCommandHandler(
        IDistributedCache cache,
        YourDbContext dbContext)
    {
        _cache = cache;
        _dbContext = dbContext;
    }

    public async Task<MyResponse> Handle(MyCommand request, CancellationToken ct)
    {
        var cacheKey = $"data:{request.Id}";

        // Try get from cache
        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (cachedData != null)
        {
            return JsonSerializer.Deserialize<MyResponse>(cachedData);
        }

        // Not in cache, get from database
        var data = await _dbContext.Data
            .Where(d => d.Id == request.Id)
            .FirstOrDefaultAsync(ct);

        // Store in cache
        var response = new MyResponse { Data = data };
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(response),
            cacheOptions,
            ct);

        return response;
    }
}
```

### Cache Helper Methods

```csharp
// Set cache with expiration
await _cache.SetStringAsync(
    key: "myKey",
    value: JsonSerializer.Serialize(data),
    options: new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
    });

// Get from cache
var cached = await _cache.GetStringAsync("myKey");
var data = cached != null ? JsonSerializer.Deserialize<MyData>(cached) : null;

// Remove from cache
await _cache.RemoveAsync("myKey");

// Refresh cache expiration
await _cache.RefreshAsync("myKey");
```

---

## 9. Multiple DbContexts in Same Service

### Scenario: Service needs access to its own DB + Identity DB

### Step 1: Add Both DbContexts

```csharp
// YourService.Infrastructure/Data/YourDbContext.cs
public class YourDbContext : DbContext, ITenantContext
{
    private readonly ITenantConfigurationProvider _tenantConfig;

    public YourDbContext(
        DbContextOptions<YourDbContext> options,
        ITenantConfigurationProvider tenantConfig) : base(options)
    {
        _tenantConfig = tenantConfig;
    }

    public DbSet<YourEntity> YourEntities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = _tenantConfig.GetConnectionString();
            optionsBuilder.UseNpgsql(connectionString);
        }
    }
}

// Reference Identity.Infrastructure package and use IdentityDbContext
```

### Step 2: Register Both in Program.cs

```csharp
using Identity.Infrastructure.Data;
using YourService.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Register YOUR service's DbContext (tenant-aware)
builder.Services.AddDatabaseContext<YourDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(YourDbContext).Assembly.GetName().Name);

// Register Identity DbContext (tenant-aware, different connection)
builder.Services.AddDbContext<IdentityDbContext>((sp, options) =>
{
    var tenantConfig = sp.GetRequiredService<ITenantConfigurationProvider>();
    var identityConnectionString = tenantConfig.GetConfigurationValue("IdentityConnectionString");

    options.UseNpgsql(identityConnectionString);
});
```

### Step 3: Configure Tenant to Provide Both Connection Strings

In Tenant Service, add both connection strings to tenant configuration:

```csharp
// When creating/updating tenant
var tenantConfig = new Dictionary<string, string>
{
    { "ConnectionString", "Host=localhost;Database=YourServiceDb_AcmeCorp;..." },
    { "IdentityConnectionString", "Host=localhost;Database=IdentityDb_AcmeCorp;..." }
};
```

### Step 4: Use Both DbContexts in Handler

```csharp
public class MyCommandHandler : IRequestHandler<MyCommand, MyResponse>
{
    private readonly YourDbContext _yourDbContext;
    private readonly IdentityDbContext _identityDbContext;

    public MyCommandHandler(
        YourDbContext yourDbContext,
        IdentityDbContext identityDbContext)
    {
        _yourDbContext = yourDbContext;
        _identityDbContext = identityDbContext;
    }

    public async Task<MyResponse> Handle(MyCommand request, CancellationToken ct)
    {
        // Access your service's data
        var yourData = await _yourDbContext.YourEntities
            .Where(e => e.Id == request.Id)
            .FirstOrDefaultAsync(ct);

        // Access Identity data (user info)
        var user = await _identityDbContext.Users
            .Where(u => u.Id == request.UserId)
            .FirstOrDefaultAsync(ct);

        // Combine data from both contexts
        return new MyResponse
        {
            Data = yourData,
            UserName = user?.UserName
        };
    }
}
```

### Alternative: Shared Database with Different Schemas

```csharp
// YourDbContext with schema
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("yourservice");

    modelBuilder.Entity<YourEntity>()
        .ToTable("YourEntities", "yourservice");
}

// IdentityDbContext with schema
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("identity");

    modelBuilder.Entity<User>()
        .ToTable("Users", "identity");
}
```

---

## 10. Localization

### Step 1: Add Required Packages

```xml
<!-- In {Service}.API.csproj -->
<PackageReference Include="IhsanDev.Shared.Localization" />
```

### Step 2: Create Resource Files

```
YourService.API/
└── Resources/
    ├── SharedResource.ar.json
    └── SharedResource.en.json
```

**SharedResource.en.json**:

```json
{
  "WelcomeMessage": "Welcome to our service",
  "ErrorNotFound": "Resource not found",
  "SuccessCreated": "Created successfully"
}
```

**SharedResource.ar.json**:

```json
{
  "WelcomeMessage": "مرحبا بكم في خدمتنا",
  "ErrorNotFound": "المورد غير موجود",
  "SuccessCreated": "تم الإنشاء بنجاح"
}
```

### Step 3: Register Localization in Program.cs

```csharp
using IhsanDev.Shared.Localization;

var builder = WebApplication.CreateBuilder(args);

// Register localization with resource path
builder.Services.AddCustomLocalization(
    resourcePath: "Resources",
    defaultCulture: "en",
    supportedCultures: new[] { "en", "ar" });

var app = builder.Build();

// Add localization middleware BEFORE authentication
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();
```

### Step 4: Use IStringLocalizer

```csharp
using Microsoft.Extensions.Localization;

public class MyCommandHandler : IRequestHandler<MyCommand, MyResponse>
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public MyCommandHandler(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    public async Task<MyResponse> Handle(MyCommand request, CancellationToken ct)
    {
        // Get localized string
        var message = _localizer["WelcomeMessage"].Value;

        // With parameters
        var errorMessage = _localizer["ErrorWithParam", request.Id].Value;

        return new MyResponse { Message = message };
    }
}
```

### Step 5: Use in Endpoints

```csharp
app.MapGet("/api/welcome", (
    IStringLocalizer<SharedResource> localizer) =>
{
    var message = localizer["WelcomeMessage"].Value;
    return Results.Ok(new { message });
});
```

### Step 6: Client Sends Accept-Language Header

```http
GET /api/welcome HTTP/1.1
Host: localhost:5001
Accept-Language: ar
```

**Response**:

```json
{
  "message": "مرحبا بكم في خدمتنا"
}
```

### Localization with Validation

```csharp
using FluentValidation;

public class MyCommandValidator : AbstractValidator<MyCommand>
{
    public MyCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(localizer["ErrorEmailRequired"].Value)
            .EmailAddress()
            .WithMessage(localizer["ErrorEmailInvalid"].Value);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(localizer["ErrorNameRequired"].Value)
            .MaximumLength(100)
            .WithMessage(localizer["ErrorNameTooLong"].Value);
    }
}
```

---

## Quick Decision Matrix

| Scenario                | Configuration                          | Key Files                                    |
| ----------------------- | -------------------------------------- | -------------------------------------------- |
| **Tenant Service**      | `MultiTenancy:Enabled=true`            | `MULTI_TENANCY_GUIDE.md`                     |
| **Standalone Service**  | `MultiTenancy:Enabled=false`           | `NEW_SERVICE_INTEGRATION_GUIDE.md`           |
| **Dual Mode**           | Conditional registration in Program.cs | This guide, Section 4                        |
| **Service-to-Service**  | `ServiceAuthentication` config         | `SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md` |
| **Internal Endpoints**  | `UseServiceAuthenticationMiddleware()` | This guide, Section 7                        |
| **Cache**               | `Redis:Enabled=true/false`             | `REDIS_ENABLED_VS_DISABLED_GUIDE.md`         |
| **Multiple DbContexts** | Register both with different configs   | This guide, Section 9                        |
| **Localization**        | `AddCustomLocalization()`              | `LOCALIZATION_GUIDE.md`                      |

---

## Related Documentation

- **Complete Service Setup**: `NEW_SERVICE_INTEGRATION_GUIDE.md`
- **Multi-Tenancy Deep Dive**: `MULTI_TENANCY_GUIDE.md`
- **Service Authentication**: `SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md`
- **Database Architecture**: `DATABASE_PER_TENANT_ARCHITECTURE.md`
- **Caching Strategy**: `CACHING_STRATEGY_COMPARISON.md`
- **Localization**: `LOCALIZATION_GUIDE.md`

---

**Last Updated**: November 2025 | **Version**: 1.0
