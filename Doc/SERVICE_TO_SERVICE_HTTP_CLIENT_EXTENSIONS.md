# Service-to-Service HTTP Client Extensions - Complete Guide

## Overview

This guide documents the reusable HTTP client extension methods for service-to-service communication across the microservices architecture. These extensions provide clean, consistent configuration for inter-service communication.

## Available Service Client Extensions

### 1. FileManager Service Client

**Location:** `IhsanDev.Shared.Infrastructure.Extensions.FileManagerServiceExtensions`

**Purpose:** Fast file operations and metadata retrieval

**Extension Method:**

```csharp
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "YourServiceName",
    builder.Environment.IsDevelopment());
```

**Configuration:**

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005",
      "Timeout": 5
    }
  }
}
```

**Used By:** Identity Service, Notification Service

---

### 2. Notification Service Client

**Location:** `IhsanDev.Shared.Infrastructure.Extensions.NotificationServiceExtensions`

**Purpose:** Sending notifications (email, SMS, push)

**Extension Method:**

```csharp
builder.Services.AddNotificationServiceClient(
    builder.Configuration,
    "YourServiceName",
    builder.Environment.IsDevelopment());
```

**Configuration:**

```json
{
  "Services": {
    "NotificationService": {
      "BaseUrl": "https://localhost:5104",
      "Timeout": 30
    }
  }
}
```

**Used By:** Identity Service, FileManager Service

---

### 3. Identity Service Client

**Location:** `IhsanDev.Shared.Infrastructure.Extensions.IdentityServiceExtensions`

**Purpose:** User authentication, device tokens, profile management

**Extension Methods:**

**Named Client:**

```csharp
builder.Services.AddIdentityServiceClient(
    builder.Configuration,
    "YourServiceName",
    builder.Environment.IsDevelopment());
```

**Typed Client:**

```csharp
builder.Services.AddIdentityServiceClient<IIdentityServiceClient, IdentityServiceClient>(
    builder.Configuration,
    "YourServiceName",
    builder.Environment.IsDevelopment());
```

**Configuration:**

```json
{
  "Services": {
    "IdentityService": {
      "BaseUrl": "https://localhost:5001",
      "Timeout": 30
    }
  }
}
```

**Used By:** Notification Service

---

### 4. Tenant Service Client

**Location:** `IhsanDev.Shared.Infrastructure.Extensions.TenantServiceExtensions`

**Purpose:** Tenant configuration and management

**Extension Methods:**

**Named Client:**

```csharp
builder.Services.AddTenantServiceClient(
    builder.Configuration,
    "YourServiceName",
    builder.Environment.IsDevelopment());
```

**Typed Client:**

```csharp
builder.Services.AddTenantServiceClient<ITenantServiceClient, TenantServiceClient>(
    builder.Configuration,
    "YourServiceName",
    builder.Environment.IsDevelopment());
```

**Configuration:**

```json
{
  "Services": {
    "TenantService": {
      "BaseUrl": "https://localhost:5002",
      "Timeout": 30
    }
  }
}
```

**Used By:** FileManager Service, Notification Service

---

## Common Configuration

### Service Authentication

All service clients automatically include authentication headers:

```json
{
  "ServiceCommunication": {
    "SharedSecret": "your-shared-secret-here",
    "Enabled": true
  }
}
```

**Headers Added:**

- `X-Service-Secret`: Shared secret for authentication
- `X-Service-Name`: Name of the calling service

### SSL Configuration

**Development:** SSL certificate validation is automatically bypassed when `isDevelopment` is `true`

**Production:** Full SSL certificate validation is enforced

---

## Usage Examples by Service

### Identity Service (Program.cs)

```csharp
using IhsanDev.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register Notification service client
builder.Services.AddNotificationServiceClient(
    builder.Configuration,
    "IdentityService",
    builder.Environment.IsDevelopment());

// Register FileManager service client
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "IdentityService",
    builder.Environment.IsDevelopment());

var app = builder.Build();
app.Run();
```

**Configuration:**

```json
{
  "Services": {
    "NotificationService": {
      "BaseUrl": "https://localhost:5104",
      "Timeout": 30
    },
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005",
      "Timeout": 5
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "dev-secret-key"
  }
}
```

---

### FileManager Service (Program.cs)

```csharp
using IhsanDev.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register Notification service client
builder.Services.AddNotificationServiceClient(
    builder.Configuration,
    "FileManagerService",
    builder.Environment.IsDevelopment());

// Register Tenant service client (typed)
builder.Services.AddTenantServiceClient<ITenantServiceClient, TenantServiceClient>(
    builder.Configuration,
    "FileManagerService",
    builder.Environment.IsDevelopment());

var app = builder.Build();
app.Run();
```

**Configuration:**

```json
{
  "Services": {
    "NotificationService": {
      "BaseUrl": "https://localhost:5104",
      "Timeout": 30
    },
    "TenantService": {
      "BaseUrl": "https://localhost:5002",
      "Timeout": 30
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "dev-secret-key"
  }
}
```

---

### Notification Service (Program.cs)

```csharp
using IhsanDev.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register Identity service client (typed)
builder.Services.AddIdentityServiceClient<IIdentityServiceClient, IdentityServiceClient>(
    builder.Configuration,
    "NotificationService",
    builder.Environment.IsDevelopment());

// Register Tenant service client (typed)
builder.Services.AddTenantServiceClient<ITenantServiceClient, TenantServiceClient>(
    builder.Configuration,
    "NotificationService",
    builder.Environment.IsDevelopment());

var app = builder.Build();
app.Run();
```

**Configuration:**

```json
{
  "Services": {
    "IdentityService": {
      "BaseUrl": "https://localhost:5001",
      "Timeout": 30
    },
    "TenantService": {
      "BaseUrl": "https://localhost:5002",
      "Timeout": 30
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "dev-secret-key"
  }
}
```

---

## Extension Method Parameters

All extension methods share the same signature:

```csharp
public static IServiceCollection Add[Service]ServiceClient(
    this IServiceCollection services,
    IConfiguration configuration,
    string serviceName,
    bool isDevelopment = false)
```

### Parameters:

| Parameter       | Type                 | Description                         | Example                               |
| --------------- | -------------------- | ----------------------------------- | ------------------------------------- |
| `services`      | `IServiceCollection` | Service collection to register with | `builder.Services`                    |
| `configuration` | `IConfiguration`     | App configuration                   | `builder.Configuration`               |
| `serviceName`   | `string`             | Name of calling service             | `"IdentityService"`                   |
| `isDevelopment` | `bool`               | Enable SSL bypass for dev           | `builder.Environment.IsDevelopment()` |

---

## Features

### ✅ Automatic Configuration

- Base URL from configuration
- Configurable timeout
- Service authentication headers
- SSL validation handling
- Connection pooling

### ✅ Consistent Behavior

- Same configuration pattern across all services
- Standardized error handling
- Automatic retry policies (via HttpClient)
- Request/response logging

### ✅ Type Safety

- Support for typed clients
- Interface-based dependency injection
- Compile-time safety

### ✅ Flexibility

- Named or typed client registration
- Environment-specific configuration
- Fallback defaults for missing config

---

## Configuration Priority

Each service client checks multiple configuration sources in this order:

1. `Services:[ServiceName]:BaseUrl`
2. `[ServiceName]:BaseUrl` (legacy)
3. Service-specific fallback (e.g., `MultiTenancy:TenantServiceUrl`)
4. Hardcoded default (e.g., `https://localhost:5001`)

**Example for Tenant Service:**

```json
{
  "Services": {
    "TenantService": {
      "BaseUrl": "https://localhost:5002" // ← Preferred
    }
  },
  "MultiTenancy": {
    "TenantServiceUrl": "https://localhost:5002" // ← Fallback
  }
}
```

---

## Environment-Specific Configuration

### Development (appsettings.Development.json)

```json
{
  "Services": {
    "NotificationService": {
      "BaseUrl": "https://localhost:5104",
      "Timeout": 30
    },
    "IdentityService": {
      "BaseUrl": "https://localhost:5001",
      "Timeout": 30
    },
    "TenantService": {
      "BaseUrl": "https://localhost:5002",
      "Timeout": 30
    },
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005",
      "Timeout": 5
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "dev-secret-key-12345"
  }
}
```

### Production (appsettings.Production.json)

```json
{
  "Services": {
    "NotificationService": {
      "BaseUrl": "https://notification-internal.production.com",
      "Timeout": 10
    },
    "IdentityService": {
      "BaseUrl": "https://identity-internal.production.com",
      "Timeout": 10
    },
    "TenantService": {
      "BaseUrl": "https://tenant-internal.production.com",
      "Timeout": 10
    },
    "FileManagerService": {
      "BaseUrl": "https://filemanager-internal.production.com",
      "Timeout": 3
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "${SERVICE_SHARED_SECRET}"
  }
}
```

---

## Migration Guide

### Before (Old Approach)

```csharp
// Verbose inline configuration
builder.Services.AddHttpClient("NotificationService", client =>
{
    var baseUrl = builder.Configuration["Services:NotificationService:BaseUrl"]
        ?? "https://localhost:5104";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    var timeout = builder.Configuration.GetValue<int>("Services:NotificationService:Timeout", 30);
    client.Timeout = TimeSpan.FromSeconds(timeout);

    var serviceSecret = builder.Configuration["ServiceCommunication:SharedSecret"];
    if (!string.IsNullOrEmpty(serviceSecret))
    {
        client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
        client.DefaultRequestHeaders.Add("X-Service-Name", "IdentityService");
    }
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});
```

### After (New Approach)

```csharp
// Clean, one-line registration
builder.Services.AddNotificationServiceClient(
    builder.Configuration,
    "IdentityService",
    builder.Environment.IsDevelopment());
```

**Benefits:**

- ✅ 20+ lines → 4 lines
- ✅ Reusable across all services
- ✅ Consistent configuration
- ✅ Easier to maintain
- ✅ Type-safe

---

## Best Practices

### ✅ DO:

1. **Use extension methods** instead of inline HttpClient configuration
2. **Set service name accurately** for logging and tracking
3. **Use typed clients** when available for type safety
4. **Configure timeout appropriately** based on service SLA
5. **Use environment-specific configuration** for different environments
6. **Enable SSL bypass only in development** via `isDevelopment` parameter

### ❌ DON'T:

1. **Don't hardcode service URLs** in code
2. **Don't bypass SSL validation in production**
3. **Don't set extremely high timeouts** (causes poor UX)
4. **Don't forget to configure SharedSecret** for authentication
5. **Don't register the same client multiple times**

---

## Troubleshooting

| Issue                                               | Solution                                      |
| --------------------------------------------------- | --------------------------------------------- |
| `InvalidOperationException: BaseUrl not configured` | Add service configuration to appsettings.json |
| 403 Forbidden                                       | Verify `SharedSecret` matches across services |
| Timeout errors                                      | Increase timeout in configuration             |
| SSL certificate errors                              | Set `isDevelopment` to `true` for local dev   |
| Service not found                                   | Check service is running and URL is correct   |

---

## Service Dependency Matrix

| Service          | Depends On                | Client Used                                                   |
| ---------------- | ------------------------- | ------------------------------------------------------------- |
| **Identity**     | FileManager, Notification | `AddFileManagerServiceClient`, `AddNotificationServiceClient` |
| **FileManager**  | Notification, Tenant      | `AddNotificationServiceClient`, `AddTenantServiceClient<T>`   |
| **Notification** | Identity, Tenant          | `AddIdentityServiceClient<T>`, `AddTenantServiceClient<T>`    |
| **Tenant**       | None                      | N/A                                                           |

---

## Complete Configuration Example

```json
{
  "Services": {
    "NotificationService": {
      "BaseUrl": "https://localhost:5104",
      "Timeout": 30
    },
    "IdentityService": {
      "BaseUrl": "https://localhost:5001",
      "Timeout": 30
    },
    "TenantService": {
      "BaseUrl": "https://localhost:5002",
      "Timeout": 30
    },
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005",
      "Timeout": 5
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "dev-secret-key-12345",
    "Enabled": true
  }
}
```

---

## Summary

**4 Extension Methods Created:**

1. ✅ `AddFileManagerServiceClient` - File operations
2. ✅ `AddNotificationServiceClient` - Notifications
3. ✅ `AddIdentityServiceClient` / `AddIdentityServiceClient<T>` - User management
4. ✅ `AddTenantServiceClient` / `AddTenantServiceClient<T>` - Tenant management

**3 Services Updated:**

1. ✅ Identity.API
2. ✅ FileManager.API
3. ✅ Notification.API

**Benefits:**

- 🎯 Clean, consistent service registration
- 🔒 Secure service-to-service authentication
- ⚡ Optimized for performance
- 🛠️ Easy to maintain and extend
- 📦 Reusable across all microservices

---

## Related Documentation

- [File Manager Guide](FILE_MANAGER.md)
- [Service-to-Service Communication Guide](SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md)
- [Bypass Tenant Endpoints Guide](BYPASS_TENANT_ENDPOINTS_GUIDE.md)
- [Multi-Tenancy Architecture](DATABASE_PER_TENANT_ARCHITECTURE.md)

---

**Last Updated:** November 22, 2025  
**Version:** 1.0
