# 🚀 New Service Integration Guide

## Complete Guide: Authentication & Tenant Data Access for New Services

This comprehensive guide explains how to integrate **Authentication** (via Identity Service) and **Tenant Configuration** (via Tenant Service) into any new microservice.

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Part 1: Authentication Integration](#part-1-authentication-integration)
4. [Part 2: Tenant Data Integration](#part-2-tenant-data-integration)
5. [Part 3: Testing Integration](#part-3-testing-integration)
6. [Complete Example: Order Service](#complete-example-order-service)
7. [Common Scenarios](#common-scenarios)
8. [Best Practices](#best-practices)
9. [Troubleshooting](#troubleshooting)

---

## Overview

### What You'll Learn

- ✅ How to add JWT authentication to your service
- ✅ How to access authenticated user information
- ✅ How to implement role-based authorization (User, Admin)
- ✅ How to integrate tenant-specific configuration (optional)
- ✅ How to access tenant data from Tenant Service
- ✅ How to write integration tests with authentication and tenants
- ✅ How to use shared testing helpers

### ⚡ Quick Answer: What You Actually Need to Do

**For Authentication** (3 steps):

1. Add JWT configuration to `appsettings.json`
2. Add `builder.Services.AddAuthentication()` and configure JWT
3. Add `app.UseAuthentication()` and `app.UseAuthorization()` to middleware

**For Multi-Tenancy** (1 step - OPTIONAL):

1. Add `builder.Services.AddMultiTenancy(configuration)`

**That's it!** The tenant middleware is **already implemented in the shared library**. You don't need to create or register it manually.

### Architecture Overview

```
┌─────────────────┐      ┌──────────────────┐      ┌─────────────────┐
│  Identity       │      │  Tenant Service  │      │  Your New       │
│  Service        │      │  (Optional)      │      │  Service        │
│                 │      │                  │      │                 │
│  • JWT Auth     │──────▶  • Tenant Config │──────▶  • Reads JWT   │
│  • User Mgmt    │      │  • Tenant Data   │      │  • Gets User    │
│  • Login/Reg    │      │  • Settings      │      │  • Uses Config  │
└─────────────────┘      └──────────────────┘      └─────────────────┘
```

### Key Endpoints You'll Use

| Service  | Endpoint                      | Purpose                         |
| -------- | ----------------------------- | ------------------------------- |
| Identity | `POST /api/auth/login`        | Get JWT access token            |
| Identity | `POST /api/auth/register`     | Create new user                 |
| Identity | `GET /api/user/profile`       | Get current user info           |
| Tenant   | `GET /api/tenants/{tenantId}` | Get tenant configuration        |
| Tenant   | `POST /api/tenants`           | Create new tenant (admin only)  |
| Your API | Any protected endpoint        | Requires `Authorization` header |

---

## Prerequisites

Before starting, ensure you have:

- ✅ .NET 9.0 SDK installed
- ✅ Identity Service running (for authentication)
- ✅ Tenant Service running (optional, for multi-tenancy)
- ✅ Basic understanding of Clean Architecture
- ✅ Understanding of JWT authentication

---

## Part 1: Authentication Integration

### Step 1: Add Required Packages

Add authentication packages to your service's `.csproj`:

```xml
<ItemGroup>
  <!-- Authentication -->
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />

  <!-- Shared libraries (if not already added) -->
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Infrastructure\IhsanDev.Shared.Infrastructure.csproj" />
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Application\IhsanDev.Shared.Application.csproj" />
</ItemGroup>
```

### Step 2: Configure JWT in appsettings.json

Add JWT configuration to `appsettings.json`:

```json
{
  "Jwt": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 21600
  }
}
```

> **Important**: Use the **same JWT settings** as your Identity Service for token validation to work.

### Step 3: Add Authentication in Program.cs

Configure authentication middleware in your `Program.cs`:

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Authentication & Authorization
// ============================================
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
});

builder.Services.AddAuthorization();

// ... rest of your service configuration

var app = builder.Build();

// ============================================
// Middleware Pipeline
// ============================================
app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();

// ... rest of your middleware

app.Run();
```

### Step 4: Protect Your Endpoints

#### Option A: Using Minimal APIs

```csharp
// Require authentication for all endpoints in a group
var ordersGroup = app.MapGroup("/api/orders")
    .RequireAuthorization(); // All endpoints require authentication

ordersGroup.MapGet("/", GetAllOrders);
ordersGroup.MapGet("/{id:int}", GetOrderById);
ordersGroup.MapPost("/", CreateOrder);

// Require specific role
var adminGroup = app.MapGroup("/api/admin/orders")
    .RequireAuthorization("Admin"); // Requires Admin role

adminGroup.MapGet("/", GetAllOrdersAdmin);
adminGroup.MapDelete("/{id:int}", DeleteOrder);
```

#### Option B: Using Handler Attributes (if using handler classes)

```csharp
// In your handler class
[Authorize] // Requires authentication
public async Task<IResult> GetAllOrders(IMediator mediator)
{
    var query = new GetAllOrdersQuery();
    var result = await mediator.Send(query);
    return Results.Ok(result);
}

[Authorize(Roles = "Admin")] // Requires Admin role
public async Task<IResult> DeleteOrder(int id, IMediator mediator)
{
    var command = new DeleteOrderCommand(id);
    await mediator.Send(command);
    return Results.NoContent();
}
```

### Step 5: Access Authenticated User Information

```csharp
using System.Security.Claims;

public class CreateOrderHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateOrderHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request)
    {
        // Get current user from JWT token claims
        var httpContext = _httpContextAccessor.HttpContext;
        var userIdClaim = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier);
        var emailClaim = httpContext?.User.FindFirst(ClaimTypes.Email);
        var roleClaim = httpContext?.User.FindFirst(ClaimTypes.Role);

        if (userIdClaim == null)
            throw new UnauthorizedException("User not authenticated");

        var userId = int.Parse(userIdClaim.Value);
        var userEmail = emailClaim?.Value;
        var userRole = roleClaim?.Value;

        // Use user info in your business logic
        var order = new Order
        {
            UserId = userId,
            CreatedBy = userEmail,
            // ... rest of order properties
        };

        // ... save order
        return orderDto;
    }
}
```

**Don't forget to register IHttpContextAccessor**:

```csharp
// In Program.cs
builder.Services.AddHttpContextAccessor();
```

### Step 6: Test Authentication

#### Get JWT Token from Identity Service

```bash
# Register a new user
curl -X POST "https://localhost:5001/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Password123!",
    "firstName": "John",
    "lastName": "Doe"
  }'

# Login to get JWT token
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Password123!"
  }'

# Response includes:
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "expiresIn": 3600
}
```

#### Call Your Protected Endpoint

```bash
curl -X GET "https://localhost:5002/api/orders" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

---

## Part 2: Tenant Data Integration

### 🎯 Important: No Manual Middleware Implementation Required!

**Key Point**: The tenant middleware is **already built and ready to use** in the shared infrastructure. You only need to call one method to enable it.

```
┌─────────────────────────────────────────────────────────┐
│  What YOU Do (1 line of code):                          │
│  ───────────────────────────────                        │
│  builder.Services.AddMultiTenancy(configuration);       │
└─────────────────────────────────────────────────────────┘
                           │
                           ↓
┌─────────────────────────────────────────────────────────┐
│  What the Shared Library Does Automatically:            │
│  ───────────────────────────────────────────            │
│  ✅ Registers tenant middleware                         │
│  ✅ Adds middleware to pipeline                         │
│  ✅ Configures caching (60 min default)                 │
│  ✅ Sets up HttpClient for Tenant Service               │
│  ✅ Registers ITenantContext service                    │
│  ✅ Registers ITenantService                            │
│  ✅ Handles tenant resolution from x-tenant-id header   │
│  ✅ Implements fallback to default configuration        │
└─────────────────────────────────────────────────────────┘
```

### When to Use Tenant Service

Use tenant service when you need:

- ✅ Per-tenant configuration (JWT secrets, database connections, etc.)
- ✅ Multi-tenant SaaS architecture
- ✅ Customer-specific settings
- ✅ Tenant isolation

**Note**: Tenant integration is **optional**. You can skip this section if you don't need multi-tenancy.

### Step 1: Add Multi-Tenancy Support (Optional)

Add multi-tenancy configuration to `appsettings.json`:

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5003",
    "CacheDurationMinutes": 60
  }
}
```

### Step 2: Register Multi-Tenancy Services

```csharp
// In Program.cs
using IhsanDev.Shared.Infrastructure.Extensions;

// Add multi-tenancy support (uses shared extension)
builder.Services.AddMultiTenancy(builder.Configuration);
```

**That's it!** This single line automatically registers everything you need:

- ✅ `ITenantContext` - Access current tenant
- ✅ `ITenantService` - Load tenant configuration from Tenant Service
- ✅ **Tenant resolution middleware** - Automatically intercepts requests and loads tenant data
- ✅ Distributed/In-memory caching - High-performance tenant configuration caching (Redis with automatic MemoryCache fallback)
- ✅ HTTP client for Tenant Service - Pre-configured HttpClient

**Important**: You **DO NOT need to manually implement or register the tenant middleware**. It's already included in the shared infrastructure (`IhsanDev.Shared.Infrastructure`) and automatically added to your middleware pipeline when you call `AddMultiTenancy()`.

### Step 3: Access Tenant Data in Your Code

#### Option A: Via ITenantContext (Recommended)

```csharp
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

public class CreateOrderHandler
{
    private readonly ITenantContext _tenantContext;

    public CreateOrderHandler(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request)
    {
        // Check if tenant is available
        if (_tenantContext.HasTenant && _tenantContext.CurrentTenant != null)
        {
            var tenantId = _tenantContext.CurrentTenant.TenantId;
            var tenantName = _tenantContext.CurrentTenant.TenantName;
            var userId = _tenantContext.CurrentTenant.UserId;

            // Access tenant-specific configuration
            var jwtConfig = _tenantContext.CurrentTenant.Configuration?.Jwt;
            var databaseConfig = _tenantContext.CurrentTenant.Configuration?.Database;

            // Use tenant data
            var order = new Order
            {
                TenantId = tenantId,
                UserId = userId,
                // ... rest of properties
            };
        }
        else
        {
            // No tenant context (non-tenant mode or tenant not found)
            // Use default behavior
        }

        return orderDto;
    }
}
```

#### Option B: Direct HTTP Call to Tenant Service

```csharp
using System.Net.Http.Json;

public class TenantDataService
{
    private readonly HttpClient _httpClient;

    public TenantDataService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("TenantService");
    }

    public async Task<TenantDto?> GetTenantByIdAsync(string tenantId)
    {
        var response = await _httpClient.GetAsync($"/api/tenants/{tenantId}");

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<TenantDto>();
    }

    public async Task<TenantDto?> GetTenantByUserIdAsync(int userId)
    {
        var response = await _httpClient.GetAsync($"/api/tenants/user/{userId}");

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<TenantDto>();
    }
}

// DTOs
public class TenantDto
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public string Data { get; set; } = "{}";
}
```

**Register HttpClient in Program.cs**:

```csharp
builder.Services.AddHttpClient("TenantService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MultiTenancy:TenantServiceUrl"] ?? "https://localhost:5003");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddScoped<TenantDataService>();
```

### Step 4: Pass Tenant ID from Frontend

#### Via HTTP Header (Recommended)

```bash
curl -X GET "https://localhost:5002/api/orders" \
  -H "Authorization: Bearer your-jwt-token" \
  -H "x-tenant-id: customer-abc-12345"
```

#### Via Query Parameter

```bash
curl -X GET "https://localhost:5002/api/orders?tenantId=customer-abc-12345" \
  -H "Authorization: Bearer your-jwt-token"
```

### Step 5: Tenant Resolution Flow

**How it works automatically** (no manual implementation needed):

```
1. Request arrives with x-tenant-id header
   ↓
2. Tenant middleware (from shared library) intercepts request automatically
   ↓
3. Middleware checks in-memory cache for tenant configuration
   ↓
4. If not cached, middleware calls Tenant Service API
   ↓
5. Middleware loads tenant config and caches it (60 min default)
   ↓
6. Middleware sets ITenantContext.CurrentTenant
   ↓
7. Your handler can access tenant via ITenantContext ✅
```

**Key Point**: The tenant middleware is **already implemented** in `IhsanDev.Shared.Infrastructure` and is **automatically registered** when you call `builder.Services.AddMultiTenancy()`. You don't need to create or register it manually in your service.

---

## Part 3: Testing Integration

### Step 1: Add Testing Packages

```xml
<ItemGroup>
  <!-- Testing packages -->
  <PackageReference Include="xunit" />
  <PackageReference Include="FluentAssertions" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />

  <!-- Shared testing infrastructure -->
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Testing\IhsanDev.Shared.Testing.csproj" />
</ItemGroup>
```

### Step 2: Create Test Factory

```csharp
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace YourService.API.Tests.Infrastructure;

public class CustomWebApplicationFactory :
    IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();

        // Add your service-specific test configuration
        config["MultiTenancy:Enabled"] = "false"; // Disable for tests
        config["Jwt:Secret"] = "test-secret-key-minimum-32-characters-long";
        config["Jwt:Issuer"] = "TestIssuer";
        config["Jwt:Audience"] = "TestAudience";

        return config;
    }
}
```

### Step 3: Create Test Base Class

```csharp
using IhsanDev.Shared.Testing.Infrastructure;

namespace YourService.API.Tests.Infrastructure;

public abstract class IntegrationTestBase :
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<YourDbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // Helper: Create authenticated request with JWT
    protected async Task<string> GetAuthTokenAsync(string email = "test@example.com")
    {
        // In real tests, you'd call Identity Service
        // For unit tests, generate a test JWT token
        return "test-jwt-token";
    }

    // Helper: Create test order
    protected async Task<Order> CreateTestOrderAsync(int userId)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync();
            return order;
        });
    }
}
```

### Step 4: Use Shared Testing Helpers

```csharp
using IhsanDev.Shared.Testing.Helpers;
using FluentAssertions;

public class OrderEndpointsTests : IntegrationTestBase
{
    public OrderEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateOrder_WithTenantContext_ShouldSucceed()
    {
        // Arrange - Generate unique IDs using shared helper
        var userId = TenantTestHelper.GenerateUniqueUserId();
        var tenantId = TenantTestHelper.GenerateUniqueTenantId("order-service");

        var createCommand = new CreateOrderCommand(
            UserId: userId,
            TenantId: tenantId,
            Items: new List<OrderItemDto> { /* ... */ }
        );

        // Act
        var result = await SendAsync(createCommand);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task CreateOrder_WithAuthentication_ShouldIncludeUserId()
    {
        // Arrange
        var userId = TenantTestHelper.GenerateUniqueUserId();

        // Set authorization header (simulating authenticated request)
        SetAuthorizationHeader($"Bearer test-token-{userId}");

        var createCommand = new CreateOrderCommand(
            Items: new List<OrderItemDto> { /* ... */ }
        );

        // Act
        var result = await SendAsync(createCommand);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
    }
}
```

### Step 5: Test with Tenant Service (HTTP-Based)

```csharp
[Fact]
public async Task CreateOrder_WithTenantFromTenantService_ShouldSucceed()
{
    // Arrange - Create user and tenant using shared helper
    var httpClient = Factory.CreateClient(); // Tenant service client

    var (userId, tenantId, tenantResponseId) =
        await TenantTestHelper.CreateUserAndTenantAsync(httpClient);

    // Act - Create order with tenant context
    var createCommand = new CreateOrderCommand(
        UserId: userId,
        TenantId: tenantId,
        Items: new List<OrderItemDto> { /* ... */ }
    );

    var result = await SendAsync(createCommand);

    // Assert
    result.Should().NotBeNull();
    result.TenantId.Should().Be(tenantId);
    result.UserId.Should().Be(userId);

    // Verify tenant exists in Tenant Service
    var tenant = await TenantTestHelper.GetTenantByIdAsync(httpClient, tenantId);
    tenant.Should().NotBeNull();
    tenant!.UserId.Should().Be(userId);
}
```

---

## Complete Example: Order Service

### Project Structure

```
Order.API/
├── Program.cs
├── appsettings.json
└── Handlers/
    └── OrderApiHandlers.cs

Order.Application/
├── Commands/
│   └── CreateOrderCommand.cs
└── Handlers/
    └── CreateOrderHandler.cs

Order.API.Tests/
├── Infrastructure/
│   ├── CustomWebApplicationFactory.cs
│   └── IntegrationTestBase.cs
└── Endpoints/
    └── OrderEndpointsTests.cs
```

### Program.cs (Complete)

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using IhsanDev.Shared.Infrastructure.Extensions;
using Order.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Authentication & Authorization
// ============================================
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
});
builder.Services.AddAuthorization();

// ============================================
// Multi-Tenancy Support (Optional)
// ============================================
builder.Services.AddMultiTenancy(builder.Configuration);

// ============================================
// Application Services
// ============================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommand).Assembly));

// Database
builder.Services.AddDatabaseContext<OrderDbContext>(
    builder.Configuration,
    migrationAssembly: typeof(OrderDbContext).Assembly.GetName().Name);

var app = builder.Build();

// ============================================
// Middleware Pipeline
// ============================================
app.UseAuthentication();
app.UseAuthorization();

// ============================================
// API Endpoints
// ============================================
var ordersGroup = app.MapGroup("/api/orders")
    .RequireAuthorization(); // All require authentication

ordersGroup.MapGet("/", GetAllOrders);
ordersGroup.MapGet("/{id:int}", GetOrderById);
ordersGroup.MapPost("/", CreateOrder);
ordersGroup.MapPut("/{id:int}", UpdateOrder);

// Admin endpoints
var adminGroup = app.MapGroup("/api/admin/orders")
    .RequireAuthorization("Admin");

adminGroup.MapGet("/", GetAllOrdersAdmin);
adminGroup.MapDelete("/{id:int}", DeleteOrder);

app.Run();

// Handler methods
async Task<IResult> GetAllOrders(IMediator mediator, HttpContext context)
{
    var query = new GetAllOrdersQuery();
    var result = await mediator.Send(query);
    return Results.Ok(result);
}

async Task<IResult> CreateOrder(CreateOrderCommand command, IMediator mediator)
{
    var result = await mediator.Send(command);
    return Results.Created($"/api/orders/{result.Id}", result);
}

// ... other handlers
```

### CreateOrderHandler.cs

```csharp
using System.Security.Claims;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly OrderDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;

    public CreateOrderHandler(
        OrderDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Get authenticated user
        var httpContext = _httpContextAccessor.HttpContext;
        var userIdClaim = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
            throw new UnauthorizedException("User not authenticated");

        var userId = int.Parse(userIdClaim.Value);

        // Get tenant context (if available)
        string? tenantId = null;
        if (_tenantContext.HasTenant && _tenantContext.CurrentTenant != null)
        {
            tenantId = _tenantContext.CurrentTenant.TenantId;
        }

        // Create order
        var order = new Order
        {
            UserId = userId,
            TenantId = tenantId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Items = request.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                Price = item.Price
            }).ToList()
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        return new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            TenantId = order.TenantId,
            OrderDate = order.OrderDate,
            Status = order.Status.ToString()
        };
    }
}
```

### OrderEndpointsTests.cs

```csharp
using FluentAssertions;
using IhsanDev.Shared.Testing.Helpers;

[Collection("Sequential")]
public class OrderEndpointsTests : IntegrationTestBase
{
    public OrderEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateOrder_WithAuthentication_ShouldSucceed()
    {
        // Arrange
        var userId = TenantTestHelper.GenerateUniqueUserId();
        var command = new CreateOrderCommand(
            Items: new List<OrderItemDto>
            {
                new(ProductId: 1, Quantity: 2, Price: 10.00m)
            }
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateOrder_WithTenant_ShouldIncludeTenantId()
    {
        // Arrange
        var userId = TenantTestHelper.GenerateUniqueUserId();
        var tenantId = TenantTestHelper.GenerateUniqueTenantId("order-service");

        var command = new CreateOrderCommand(
            TenantId: tenantId,
            Items: new List<OrderItemDto>
            {
                new(ProductId: 1, Quantity: 2, Price: 10.00m)
            }
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenantId);
    }
}
```

---

## Common Scenarios

### Scenario 1: Service Without Tenants (Simple)

**Configuration**:

```json
{
  "MultiTenancy": { "Enabled": false },
  "Jwt": {
    "Secret": "your-secret",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp"
  }
}
```

**Code**:

```csharp
// Only handle authentication, no tenant logic
var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);

var order = new Order { UserId = userId };
```

### Scenario 2: Service With Optional Tenants

**Configuration**:

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://tenant-service"
  }
}
```

**Code**:

```csharp
// Handle both tenant and non-tenant requests
var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);

string? tenantId = null;
if (_tenantContext.HasTenant)
{
    tenantId = _tenantContext.CurrentTenant?.TenantId;
}

var order = new Order
{
    UserId = userId,
    TenantId = tenantId // null for non-tenant requests
};
```

### Scenario 3: Service Requiring Tenants

**Configuration**:

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "RequireTenant": true,
    "TenantServiceUrl": "https://tenant-service"
  }
}
```

**Code**:

```csharp
// Require tenant for all requests
if (!_tenantContext.HasTenant || _tenantContext.CurrentTenant == null)
    throw new BadRequestException("Tenant ID is required");

var tenantId = _tenantContext.CurrentTenant.TenantId;
var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);

var order = new Order
{
    UserId = userId,
    TenantId = tenantId // Always required
};
```

---

## Best Practices

### Authentication

1. ✅ **Always validate JWT tokens**: Use `RequireAuthorization()` on protected endpoints
2. ✅ **Use HTTPS in production**: Never send JWT tokens over HTTP
3. ✅ **Handle token expiration**: Implement refresh token logic in your client
4. ✅ **Use role-based authorization**: Separate user and admin endpoints
5. ✅ **Store secrets securely**: Use environment variables or Azure Key Vault

### Tenant Data

1. ✅ **Cache tenant configuration**: Use distributed Redis cache or in-memory cache fallback (already implemented in shared library)
2. ✅ **Handle missing tenants gracefully**: Fall back to default configuration
3. ✅ **Validate tenant ownership**: Ensure user belongs to the tenant
4. ✅ **Log tenant context**: Include tenant ID in all logs for tracing
5. ✅ **Test both modes**: Test with and without tenants

### Testing

1. ✅ **Use shared helpers**: Leverage `TenantTestHelper` for consistency
2. ✅ **Generate unique IDs**: Avoid test conflicts with `GenerateUniqueUserId()`
3. ✅ **Test authentication**: Include tests for unauthorized access
4. ✅ **Test authorization**: Verify role-based access control
5. ✅ **Test tenant isolation**: Ensure tenant data doesn't leak

---

## Frequently Asked Questions (FAQ)

### Q1: Do I need to implement tenant middleware in every service?

**A: No!** The tenant middleware is already implemented in the shared infrastructure (`IhsanDev.Shared.Infrastructure`).

**All you need to do**:

```csharp
// In Program.cs
builder.Services.AddMultiTenancy(builder.Configuration);
```

This single line automatically:

- ✅ Registers the tenant middleware
- ✅ Adds it to your middleware pipeline
- ✅ Configures caching and HTTP client
- ✅ Sets up ITenantContext and ITenantService

You **do not** need to manually create, implement, or register the middleware.

### Q2: Is multi-tenancy required for my service?

**A: No, it's completely optional.**

- **Without multi-tenancy**: Your service works normally with authentication only
- **With multi-tenancy**: Your service can access tenant-specific configuration

You control this with configuration:

```json
{
  "MultiTenancy": { "Enabled": false } // No tenant features
}
```

### Q3: What's the difference between ITenantContext and calling Tenant Service directly?

**ITenantContext (Recommended)**:

- ✅ Automatically populated by middleware
- ✅ Cached (high performance)
- ✅ Available in all handlers
- ✅ No HTTP calls needed
- ✅ Thread-safe

**Direct HTTP calls**:

- ❌ Manual HTTP requests
- ❌ No caching (slower)
- ❌ More code to write
- ✅ Good for background jobs without HTTP context

**Use ITenantContext in 99% of cases.**

### Q4: Do I need to pass tenant ID in every request?

**A: Only if multi-tenancy is enabled and you want tenant-specific behavior.**

- **With tenant**: Include `x-tenant-id` header → tenant-specific config is used
- **Without tenant**: Omit header → default configuration is used

The service works fine either way.

### Q5: How do I test without setting up Identity and Tenant services?

**A: Use the shared test helpers.**

```csharp
// Generate test data without real services
var userId = TenantTestHelper.GenerateUniqueUserId();
var tenantId = TenantTestHelper.GenerateUniqueTenantId();

// Create test command
var command = new CreateOrderCommand(UserId: userId, TenantId: tenantId);
var result = await SendAsync(command); // Calls handler directly via MediatR
```

Tests bypass HTTP layer and call handlers directly, so you don't need Identity/Tenant services running.

### Q6: What happens if Tenant Service is down?

**A: Your service continues to work with default configuration.**

The multi-tenancy implementation includes:

- ✅ Automatic fallback to appsettings.json
- ✅ Cached tenant data (continues working during outages)
- ✅ Graceful error handling
- ✅ Logging for troubleshooting

Your service is **resilient** and won't crash if Tenant Service is unavailable.

### Q7: Can I use different JWT secrets per tenant?

**A: Yes, that's one of the main benefits of multi-tenancy.**

When tenant context is available, JWT validation automatically uses tenant-specific secrets:

```csharp
// In Identity Service Program.cs (already implemented)
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var tenantContext = context.HttpContext.RequestServices.GetService<ITenantContext>();
        if (tenantContext?.HasTenant == true)
        {
            // Use tenant-specific JWT secret
            var tenantJwt = tenantContext.CurrentTenant.Configuration.Jwt;
            context.Options.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tenantJwt.Secret));
        }
        return Task.CompletedTask;
    }
};
```

This is **already implemented** in the shared infrastructure for Identity Service.

### Q8: How do I know which shared libraries to reference?

**Minimum required** for any service:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Kernel\..." />
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Application\..." />
  <ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Infrastructure\..." />
</ItemGroup>
```

**For authentication**:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
```

**For testing**:

```xml
<ProjectReference Include="..\..\..\Shared\IhsanDev.Shared.Testing\..." />
```

Look at Identity or Tenant service `.csproj` files as examples.

### Q9: Where is the tenant middleware code located?

**Location**: `src/Shared/IhsanDev.Shared.Infrastructure/Middleware/`

You can look at the implementation for reference, but you **don't need to modify it**. Just call `AddMultiTenancy()` and it works.

### Q10: Can I customize tenant resolution (e.g., use subdomain instead of header)?

**A: Yes, but you need to modify the shared middleware.**

The default implementation uses the `x-tenant-id` header. To use subdomain:

1. Modify `TenantResolutionMiddleware` in shared infrastructure
2. Extract tenant from `context.Request.Host.Host` instead of header
3. Rebuild shared libraries
4. All services automatically use the new logic

**Note**: This affects all services, so coordinate with your team.

---

## Troubleshooting

### Issue 1: 401 Unauthorized

**Symptom**: All requests return 401 Unauthorized

**Solutions**:

- ✅ Check JWT token is included in `Authorization: Bearer <token>` header
- ✅ Verify JWT secret matches Identity Service configuration
- ✅ Ensure `UseAuthentication()` is before `UseAuthorization()` in middleware pipeline
- ✅ Check token hasn't expired

### Issue 2: Tenant Not Found

**Symptom**: `ITenantContext.CurrentTenant` is null

**Solutions**:

- ✅ Verify `x-tenant-id` header is included in request
- ✅ Check Tenant Service is running and accessible
- ✅ Verify tenant exists in Tenant Service database
- ✅ Check `MultiTenancy:Enabled` is `true` in configuration

### Issue 3: Token Validation Failed

**Symptom**: `IDX10503: Signature validation failed`

**Solutions**:

- ✅ Ensure JWT secret is **exactly the same** in all services
- ✅ Check secret is at least 32 characters long
- ✅ Verify `Issuer` and `Audience` match Identity Service configuration

### Issue 4: User ID Not Found in Token

**Symptom**: `ClaimTypes.NameIdentifier` is null

**Solutions**:

- ✅ Verify Identity Service includes user ID in JWT token
- ✅ Check claim type matches (might be "sub" or "nameid")
- ✅ Use `httpContext.User.Claims` to see all available claims

---

## Additional Resources

### Documentation

- 📖 [Multi-Tenancy Guide](MULTI_TENANCY_GUIDE.md) - Comprehensive multi-tenancy documentation
- 🚀 [Multi-Tenancy Quick Start](MULTI_TENANCY_QUICK_START.md) - Get started quickly
- 📋 [Identity Service README](../src/Services/Identity/README.md) - Identity service details
- 📋 [Multi-Tenancy Guide](MULTI_TENANCY_GUIDE.md) - Tenant service patterns and configuration
- 🧪 [TenantTestHelper Guide](../src/Shared/IhsanDev.Shared.Testing/Helpers/README_TENANT_HELPER.md) - Testing helper documentation

### Example Projects

- 🔐 **Identity Service**: `src/Services/Identity/` - Authentication implementation
- 🏢 **Tenant Service**: `src/Services/Tenant/` - Tenant management implementation
- 🧪 **Identity Tests**: `src/Services/Identity/Identity.API.Tests/` - 35 integration tests
- 🧪 **Tenant Tests**: `src/Services/Tenant/Tenant.API.Tests/` - 42 integration tests

### API Endpoints Reference

#### Identity Service (Port 5001)

- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login and get JWT token
- `GET /api/user/profile` - Get current user profile
- `GET /api/admin/users` - Get all users (Admin only)

#### Tenant Service (Port 5003)

- `GET /api/tenants/{tenantId}` - Get tenant by ID
- `GET /api/tenants/user/{userId}` - Get tenant by user ID
- `POST /api/tenants` - Create new tenant
- `GET /api/tenants` - Get all active tenants (paginated)

---

## Summary Checklist

When creating a new service, ensure you:

- [ ] Add JWT authentication packages and configuration
- [ ] Configure authentication middleware in Program.cs
- [ ] Protect endpoints with `RequireAuthorization()`
- [ ] Access authenticated user via `IHttpContextAccessor`
- [ ] Add multi-tenancy support (if needed)
- [ ] Access tenant data via `ITenantContext`
- [ ] Create test infrastructure (Factory, TestBase)
- [ ] Use `TenantTestHelper` for testing
- [ ] Test authentication and authorization
- [ ] Test tenant isolation (if using multi-tenancy)
- [ ] Document your API endpoints
- [ ] Update service README with auth/tenant details

---

## Quick Reference Card

### 📋 Authentication Setup (Required)

```csharp
// 1. appsettings.json
{
  "Jwt": {
    "Secret": "your-secret-minimum-32-chars",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp"
  }
}

// 2. Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* configure TokenValidationParameters */ });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

app.UseAuthentication();
app.UseAuthorization();

// 3. Protect endpoints
app.MapGroup("/api/orders").RequireAuthorization();

// 4. Access user in handler
var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
```

### 🏢 Multi-Tenancy Setup (Optional)

```csharp
// 1. appsettings.json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5003"
  }
}

// 2. Program.cs - ONE LINE!
builder.Services.AddMultiTenancy(builder.Configuration);
// ✅ That's it! Middleware is automatically registered

// 3. Access tenant in handler
if (_tenantContext.HasTenant)
{
    var tenantId = _tenantContext.CurrentTenant.TenantId;
}
```

### 🧪 Testing Setup

```csharp
// 1. Reference shared testing
<ProjectReference Include="...\IhsanDev.Shared.Testing\..." />

// 2. Use shared helpers
using IhsanDev.Shared.Testing.Helpers;

var userId = TenantTestHelper.GenerateUniqueUserId();
var tenantId = TenantTestHelper.GenerateUniqueTenantId("my-service");
```

### ❓ Common Questions

| Question                             | Answer                             |
| ------------------------------------ | ---------------------------------- |
| Do I implement tenant middleware?    | **No** - Already in shared library |
| Is multi-tenancy required?           | **No** - Completely optional       |
| Can I test without Identity Service? | **Yes** - Use TenantTestHelper     |
| What if Tenant Service is down?      | Falls back to default config       |
| How to pass tenant ID?               | Via `x-tenant-id` header           |

---

**Built with ❤️ for the Microservices Architecture**

For questions or issues, refer to the [main README](README.md) or create an issue on GitHub.
