# 📄 Data Property Migration Summary

## Overview

This document summarizes the migration from using **string-based Data property** to **strongly-typed TenantConfiguration object** for tenant endpoints.

---

## ✅ What Changed

### Before (Old Implementation)

```csharp
// Commands used string for Data property
public record CreateTenantCommand(
    string TenantId,
    string TenantName,
    int UserId,
    DateTime StartDate,
    DateTime? ExpireDate,
    string Data  // ❌ JSON string
) : IRequest<TenantDto>;

// API requests sent JSON as string
{
  "tenantId": "tenant-123",
  "tenantName": "Test Corp",
  "data": "{\"jwt\":{\"secret\":\"...\"},\"database\":{...}}"  // ❌ Escaped JSON string
}
```

### After (New Implementation)

```csharp
// Commands use TenantConfiguration object
public record CreateTenantCommand(
    string TenantId,
    string TenantName,
    int UserId,
    DateTime StartDate,
    DateTime? ExpireDate,
    TenantConfiguration Data  // ✅ Strongly-typed object
) : IRequest<TenantDto>;

// API requests send JSON object
{
  "tenantId": "tenant-123",
  "tenantName": "Test Corp",
  "data": {  // ✅ JSON object (no escaping needed)
    "jwt": {
      "secret": "...",
      "issuer": "...",
      "audience": "..."
    },
    "database": {
      "provider": "PostgreSql",
      "connectionString": "..."
    },
    "cors": {
      "allowedOrigins": ["https://example.com"]
    },
    "otp": {
      "expiryInMinutes": 5,
      "maxAttempts": 5,
      "lockoutDurationInMinutes": 30
    }
  }
}
```

---

## 📊 Complete Data Flow

### User → API (JSON Input)

```json
POST /api/admin/tenant
Content-Type: application/json

{
  "tenantId": "tenant-123",
  "data": {
    "jwt": { "secret": "...", ... },
    "database": { "connectionString": "...", ... }
  }
}
```

**Handling:** ASP.NET Core automatically deserializes JSON → `TenantConfiguration` object using `System.Text.Json` with:

- `PropertyNameCaseInsensitive = true` (accepts both camelCase and PascalCase)
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` (outputs camelCase)

---

### API → Command (Object)

```csharp
// In TenantApiHandlers.cs
public static async Task<IResult> CreateTenantHandler(
    CreateTenantCommand command,  // command.Data is TenantConfiguration object
    IMediator mediator,
    CancellationToken cancellationToken)
{
    var result = await mediator.Send(command, cancellationToken);
    return Results.Created($"/api/admin/tenant/{result.TenantId}", result);
}
```

**Handling:** MediatR passes the command with `TenantConfiguration Data` property directly to the handler.

---

### Handler → Database (JSON String Storage)

```csharp
// In CreateTenantCommandHandler.cs
public async Task<TenantDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
{
    // Serialize TenantConfiguration object to JSON string for database storage
    var dataJson = JsonSerializer.Serialize(request.Data, new JsonSerializerOptions
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    var tenant = new TenantSettings
    {
        TenantId = request.TenantId,
        TenantName = request.TenantName,
        UserId = request.UserId,
        Data = dataJson,  // Store as JSON string in PostgreSQL
        StartDate = request.StartDate,
        ExpireDate = request.ExpireDate
    };

    await _context.TenantSettings.AddAsync(tenant, cancellationToken);
    await _context.SaveChangesAsync(cancellationToken);

    return _mapper.Map<TenantDto>(tenant);
}
```

**Handling:** `JsonSerializer.Serialize()` converts `TenantConfiguration` object → JSON string for PostgreSQL TEXT column.

---

### Database Storage (PostgreSQL)

```sql
-- TenantSettings table
CREATE TABLE tenant_settings (
    tenant_id VARCHAR(50) PRIMARY KEY,
    tenant_name VARCHAR(255) NOT NULL,
    user_id INT NOT NULL,
    data TEXT NOT NULL,  -- JSON string stored here
    is_active BOOLEAN DEFAULT TRUE,
    start_date TIMESTAMP NOT NULL,
    expire_date TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

-- Example data
INSERT INTO tenant_settings (tenant_id, tenant_name, user_id, data)
VALUES (
    'tenant-123',
    'Test Corp',
    1,
    '{"jwt":{"secret":"...","issuer":"..."},"database":{"provider":"PostgreSql","connectionString":"..."},"cors":{"allowedOrigins":["https://example.com"]},"otp":{"expiryInMinutes":5}}'
);
```

**Handling:** PostgreSQL stores `data` as TEXT (JSON string).

---

### Database → DTO (Object)

```csharp
// In TenantDtos.cs
public class TenantConfigDto
{
    public required string TenantId { get; set; }
    public required string TenantName { get; set; }
    public int UserId { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? ExpireDate { get; set; }
    public TenantConfiguration? Data { get; set; }  // Strongly-typed object
}

// AutoMapper configuration
CreateMap<TenantSettings, TenantConfigDto>()
    .ForMember(dest => dest.Data, opt => opt.MapFrom(src => DeserializeData(src.Data)));

private static TenantConfiguration? DeserializeData(string data)
{
    if (string.IsNullOrWhiteSpace(data))
        return null;

    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    return JsonSerializer.Deserialize<TenantConfiguration>(data, options);
}
```

**Handling:** AutoMapper uses custom deserialization method to convert JSON string → `TenantConfiguration` object.

---

### DTO → User (JSON Response)

```json
GET /api/tenant/config/tenant-123

Response:
{
  "tenantId": "tenant-123",
  "tenantName": "Test Corp",
  "userId": 1,
  "isActive": true,
  "startDate": "2025-01-01T00:00:00Z",
  "expireDate": "2026-01-01T00:00:00Z",
  "data": {
    "jwt": {
      "secret": "...",
      "issuer": "...",
      "audience": "...",
      "accessTokenExpirationMinutes": 60,
      "refreshTokenExpirationDays": 7
    },
    "database": {
      "provider": "PostgreSql",
      "connectionString": "..."
    },
    "cors": {
      "allowedOrigins": ["https://example.com"]
    },
    "otp": {
      "expiryInMinutes": 5,
      "maxAttempts": 5,
      "lockoutDurationInMinutes": 30
    }
  }
}
```

**Handling:** ASP.NET Core automatically serializes `TenantConfiguration Data` property → JSON object using `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.

---

## 🔄 Middleware Data Flow

### TenantConfigurationProvider (Identity Service)

```csharp
// In TenantConfigurationProvider.cs
public async Task<TenantInfo?> GetTenantConfigurationAsync(string tenantId)
{
    // Check cache first
    if (_cache.TryGetValue($"tenant_config_{tenantId}", out TenantInfo? cachedTenant))
    {
        return cachedTenant;
    }

    // Fetch from Tenant Service API
    var response = await _httpClient.GetAsync($"/api/tenant/config/{tenantId}");

    if (!response.IsSuccessStatusCode)
        return null;

    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    var tenantConfigResponse = await response.Content.ReadFromJsonAsync<TenantConfigResponse>(options);

    if (tenantConfigResponse == null)
        return null;

    var tenantInfo = ParseTenantInfo(tenantConfigResponse);

    // Cache for 30 minutes
    _cache.Set($"tenant_config_{tenantId}", tenantInfo, TimeSpan.FromMinutes(30));

    return tenantInfo;
}

private class TenantConfigResponse
{
    public string? TenantId { get; set; }
    public string? TenantName { get; set; }
    public int UserId { get; set; }
    public bool IsActive { get; set; }
    public TenantConfiguration? Data { get; set; }  // ✅ Receives object (not string)
}

private TenantInfo ParseTenantInfo(TenantConfigResponse response)
{
    return new TenantInfo
    {
        TenantId = response.TenantId ?? string.Empty,
        TenantName = response.TenantName,
        UserId = response.UserId,
        IsActive = response.IsActive,
        Configuration = response.Data  // ✅ Direct assignment (no deserialization needed)
    };
}
```

**Key Change:** `TenantConfigResponse.Data` changed from `string` to `TenantConfiguration?`. The API returns a deserialized object, so no manual deserialization is needed in middleware.

---

## 📝 Files Modified

### Production Code

1. ✅ **CreateTenantCommand.cs** - `Data` property changed from `string` to `TenantConfiguration`
2. ✅ **UpdateTenantCommand.cs** - `Data` property changed from `string` to `TenantConfiguration`
3. ✅ **TenantDtos.cs** - Added `DeserializeData()` method with proper `JsonSerializerOptions`
4. ✅ **CreateTenantCommandHandler.cs** - Added JSON serialization before database save
5. ✅ **UpdateTenantCommandHandler.cs** - Added JSON serialization before database save
6. ✅ **TenantConfigurationProvider.cs** - Changed `TenantConfigResponse.Data` from `string` to `TenantConfiguration?`

### Test Files

7. ✅ **IntegrationTestBase.cs** - Added `CreateDefaultTenantConfiguration()` helper method
8. ✅ **TenantEndpointsTests.cs** - Updated all 12 tests to use `TenantConfiguration` objects
9. ✅ **AdminTenantEndpointsTests.cs** - Updated all 15 tests to use `TenantConfiguration` objects
10. ✅ **SharedHelperIntegrationTests.cs** - Updated all 3 tests to use `TenantConfiguration` objects

### Documentation

11. ✅ **MULTI_TENANCY_GUIDE.md** - Updated tenant creation example to show JSON object (not string)
12. ✅ **DATABASE_PER_TENANT_ARCHITECTURE.md** - Added clarification about data storage
13. ✅ **ADDING_VARIABLES_TO_TENANT_CONFIGURATION.md** - New comprehensive guide for adding properties

### Unchanged Files (Data Layer)

- **TenantSettings.cs** (Domain Entity) - `Data` property remains `string` for PostgreSQL storage
- **TenantInfo.cs** - `Configuration` property remains `TenantConfiguration?` (unchanged)

---

## ✅ Verification Checklist

### Compilation

- [x] ✅ All production code compiles (0 errors)
- [x] ✅ All test code compiles (0 errors)
- [x] ✅ Identity.API builds successfully
- [x] ✅ IhsanDev.Shared.Infrastructure builds successfully

### Tests

- [x] ✅ All 41 integration tests passing (0 failures)
  - [x] ✅ TenantEndpointsTests: 12/12 passing
  - [x] ✅ AdminTenantEndpointsTests: 15/15 passing
  - [x] ✅ SharedHelperIntegrationTests: 3/3 passing

### Integration

- [x] ✅ Identity service middleware can resolve tenant configurations
- [x] ✅ TenantConfigurationProvider receives `TenantConfiguration` object from API
- [x] ✅ No manual JSON deserialization needed in middleware

### Data Flow

- [x] ✅ Users send JSON object (not string) for `data` property
- [x] ✅ API accepts `TenantConfiguration` object (not string)
- [x] ✅ Commands use `TenantConfiguration` object (not string)
- [x] ✅ Handlers serialize to JSON string only for database save
- [x] ✅ Database stores as TEXT (JSON string)
- [x] ✅ DTOs deserialize to `TenantConfiguration` object for response
- [x] ✅ API returns JSON object (not string) for `data` property
- [x] ✅ Middleware receives `TenantConfiguration` object (not string)

---

## 🔍 Key Insights

### 1. No String Exposure to Users

**Confirmed:** Users **NEVER** interact with the `data` property as a string. All user-facing APIs use `TenantConfiguration` objects:

- ✅ Create Tenant: `POST /api/admin/tenant` accepts JSON object
- ✅ Update Tenant: `PUT /api/admin/tenant/{tenantId}` accepts JSON object
- ✅ Get Tenant: `GET /api/tenant/config/{tenantId}` returns JSON object
- ✅ Middleware: `TenantConfigurationProvider` receives JSON object from API

### 2. String Only Used for Database Storage

The **only** place where `data` is a string is in the PostgreSQL `TenantSettings` table:

```csharp
// Domain Entity (Database Layer)
public class TenantSettings
{
    public required string TenantId { get; set; }
    public required string TenantName { get; set; }
    public int UserId { get; set; }
    public required string Data { get; set; }  // ← Only here is it a string
    // ...
}
```

This is necessary because PostgreSQL stores JSON as TEXT, and Entity Framework maps it to `string`.

### 3. Automatic Serialization/Deserialization

```
User Input (JSON) → ASP.NET Core → TenantConfiguration object
                       ↓
          TenantConfiguration object → Handler
                       ↓
          JsonSerializer.Serialize() → JSON string → PostgreSQL
                       ↓
          PostgreSQL JSON string → AutoMapper → TenantConfiguration object
                       ↓
          TenantConfiguration object → ASP.NET Core → JSON response
```

---

## 📚 Related Documentation

- **ADDING_VARIABLES_TO_TENANT_CONFIGURATION.md** - How to add new properties to TenantConfiguration
- **MULTI_TENANCY_GUIDE.md** - Complete multi-tenancy architecture overview
- **DATABASE_PER_TENANT_ARCHITECTURE.md** - Database isolation strategy

---

## 🎉 Summary

**Before Migration:**

- Users sent JSON strings (escaped, hard to read)
- No type safety in commands
- Manual JSON parsing required

**After Migration:**

- Users send/receive clean JSON objects
- Full IntelliSense and type safety
- Automatic serialization/deserialization
- No string handling in user-facing code

**Result:** ✅ Cleaner API, better developer experience, no breaking changes for database layer!

---

**Last Updated:** January 30, 2025  
**Version:** 1.0.0
