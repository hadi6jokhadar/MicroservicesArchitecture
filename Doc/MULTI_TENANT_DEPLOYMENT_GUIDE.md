# 🚀 Multi-Tenant Deployment Guide

## Single Binary, Multiple Modes! ✨

The Identity Service is designed to work in **both tenant and non-tenant modes using the same compiled code**. You don't need separate builds!

## 🎯 How It Works (Already Implemented!)

### The Magic: Configuration-Based Mode Switching

The `JwtTokenGenerator` has a built-in **fallback mechanism**:

```csharp
private (string Secret, string Issuer, string Audience, int ExpirationMinutes) GetJwtSettings()
{
    // 1️⃣ Try tenant configuration first (if multi-tenancy is enabled)
    if (_tenantContext.HasTenant && _tenantContext.CurrentTenant?.Configuration?.Jwt != null)
    {
        var tenantJwt = _tenantContext.CurrentTenant.Configuration.Jwt;
        if (!string.IsNullOrEmpty(tenantJwt.Secret))
        {
            return (tenantJwt.Secret, tenantJwt.Issuer, ...);
        }
    }

    // 2️⃣ Fallback to appsettings.json (always works)
    return (
        _configuration["Jwt:Secret"]!,
        _configuration["Jwt:Issuer"]!,
        ...
    );
}
```

**This means:**

- ✅ If multi-tenancy is **disabled** → uses `appsettings.json`
- ✅ If multi-tenancy is **enabled** but no tenant header → uses `appsettings.json`
- ✅ If multi-tenancy is **enabled** and tenant header present → uses tenant config

## 📦 Deployment Scenarios

### Scenario 1: Project A (Without Tenants)

**appsettings.json for Project A**:

```json
{
  "MultiTenancy": {
    "Enabled": false
  },
  "Jwt": {
    "Secret": "project-a-secret-key-256-bits",
    "Issuer": "ProjectA-IdentityService",
    "Audience": "ProjectA-App",
    "AccessTokenExpirationMinutes": 60
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=projecta-db;Database=IdentityDb;..."
  }
}
```

**Behavior**:

- ✅ Multi-tenancy middleware is **bypassed**
- ✅ All requests use Project A's JWT settings
- ✅ Single database for all users
- ✅ No tenant resolution overhead

### Scenario 2: Project B (With Tenants)

**appsettings.json for Project B**:

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://projectb-tenant-service:5002",
    "CacheExpirationMinutes": 5
  },
  "Jwt": {
    "Secret": "project-b-default-secret-key-256-bits",
    "Issuer": "ProjectB-IdentityService",
    "Audience": "ProjectB-App",
    "AccessTokenExpirationMinutes": 60
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=projectb-db;Database=IdentityDb;..."
  }
}
```

**Behavior**:

- ✅ Multi-tenancy middleware is **active**
- ✅ Requests with `x-tenant-id` header use tenant-specific config
- ✅ Requests without header use default JWT settings (fallback)
- ✅ Tenant configs are cached for performance

## 🎨 Best Practice: Configuration per Environment

### Recommended Approach

Use **environment-specific configuration files**:

```
Identity.API/
├── appsettings.json                      # Base configuration
├── appsettings.Development.json          # Local dev (no tenants)
├── appsettings.ProjectA.Production.json  # Project A deployment
└── appsettings.ProjectB.Production.json  # Project B deployment
```

### Example: appsettings.ProjectA.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "MultiTenancy": {
    "Enabled": false
  },
  "Jwt": {
    "Secret": "{{ PROJECT_A_JWT_SECRET_FROM_KEYVAULT }}",
    "Issuer": "ProjectA-IdentityService",
    "Audience": "ProjectA-WebApp",
    "AccessTokenExpirationMinutes": 60
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "{{ PROJECT_A_DB_CONNECTION_FROM_KEYVAULT }}"
  },
  "Cors": {
    "AllowedOrigins": ["https://projecta.com", "https://app.projecta.com"]
  }
}
```

### Example: appsettings.ProjectB.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://tenant-service.projectb.internal:5002",
    "CacheExpirationMinutes": 10
  },
  "Jwt": {
    "Secret": "{{ PROJECT_B_DEFAULT_JWT_SECRET_FROM_KEYVAULT }}",
    "Issuer": "ProjectB-IdentityService",
    "Audience": "ProjectB-Platform",
    "AccessTokenExpirationMinutes": 120
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "{{ PROJECT_B_DB_CONNECTION_FROM_KEYVAULT }}"
  },
  "Cors": {
    "AllowedOrigins": ["https://projectb.com", "https://*.projectb.com"]
  }
}
```

## 🐳 Docker Deployment

### Single Image, Different Configurations

**Dockerfile** (same for both projects):

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Identity.API/Identity.API.csproj", "Identity.API/"]
COPY ["Identity.Application/Identity.Application.csproj", "Identity.Application/"]
COPY ["Identity.Domain/Identity.Domain.csproj", "Identity.Domain/"]
COPY ["Identity.Infrastructure/Identity.Infrastructure.csproj", "Identity.Infrastructure/"]
RUN dotnet restore "Identity.API/Identity.API.csproj"
COPY . .
WORKDIR "/src/Identity.API"
RUN dotnet build "Identity.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Identity.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Identity.API.dll"]
```

### Docker Compose for Project A (No Tenants)

**docker-compose.projecta.yml**:

```yaml
version: "3.8"

services:
  identity-api:
    image: identity-service:latest
    container_name: projecta-identity-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=https://+:443;http://+:80
      - MultiTenancy__Enabled=false
      - Jwt__Secret=${PROJECT_A_JWT_SECRET}
      - Jwt__Issuer=ProjectA-IdentityService
      - Jwt__Audience=ProjectA-App
      - DatabaseSettings__ConnectionString=${PROJECT_A_DB_CONNECTION}
    volumes:
      - ./appsettings.ProjectA.Production.json:/app/appsettings.Production.json:ro
    ports:
      - "5001:80"
      - "5002:443"
    networks:
      - projecta-network

networks:
  projecta-network:
    driver: bridge
```

### Docker Compose for Project B (With Tenants)

**docker-compose.projectb.yml**:

```yaml
version: "3.8"

services:
  tenant-service:
    image: tenant-service:latest
    container_name: projectb-tenant-service
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DatabaseSettings__ConnectionString=${PROJECT_B_TENANT_DB_CONNECTION}
    ports:
      - "5002:80"
    networks:
      - projectb-network

  identity-api:
    image: identity-service:latest
    container_name: projectb-identity-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=https://+:443;http://+:80
      - MultiTenancy__Enabled=true
      - MultiTenancy__TenantServiceUrl=http://tenant-service:80
      - MultiTenancy__CacheExpirationMinutes=10
      - Jwt__Secret=${PROJECT_B_DEFAULT_JWT_SECRET}
      - Jwt__Issuer=ProjectB-IdentityService
      - Jwt__Audience=ProjectB-Platform
      - DatabaseSettings__ConnectionString=${PROJECT_B_DB_CONNECTION}
    volumes:
      - ./appsettings.ProjectB.Production.json:/app/appsettings.Production.json:ro
    ports:
      - "5001:80"
      - "5003:443"
    depends_on:
      - tenant-service
    networks:
      - projectb-network

networks:
  projectb-network:
    driver: bridge
```

## ☸️ Kubernetes Deployment

### Project A ConfigMap (No Tenants)

**identity-configmap-projecta.yaml**:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: identity-config-projecta
  namespace: projecta
data:
  appsettings.Production.json: |
    {
      "MultiTenancy": {
        "Enabled": false
      },
      "Jwt": {
        "Issuer": "ProjectA-IdentityService",
        "Audience": "ProjectA-App",
        "AccessTokenExpirationMinutes": 60
      },
      "Cors": {
        "AllowedOrigins": ["https://projecta.com"]
      }
    }
```

### Project B ConfigMap (With Tenants)

**identity-configmap-projectb.yaml**:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: identity-config-projectb
  namespace: projectb
data:
  appsettings.Production.json: |
    {
      "MultiTenancy": {
        "Enabled": true,
        "TenantServiceUrl": "http://tenant-service.projectb.svc.cluster.local",
        "CacheExpirationMinutes": 10
      },
      "Jwt": {
        "Issuer": "ProjectB-IdentityService",
        "Audience": "ProjectB-Platform",
        "AccessTokenExpirationMinutes": 120
      },
      "Cors": {
        "AllowedOrigins": ["https://projectb.com", "https://*.projectb.com"]
      }
    }
```

### Deployment Manifest (Same for Both)

**identity-deployment.yaml**:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: identity-api
  namespace: { { NAMESPACE } }
spec:
  replicas: 3
  selector:
    matchLabels:
      app: identity-api
  template:
    metadata:
      labels:
        app: identity-api
    spec:
      containers:
        - name: identity-api
          image: your-registry/identity-service:latest
          ports:
            - containerPort: 80
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: Jwt__Secret
              valueFrom:
                secretKeyRef:
                  name: identity-secrets
                  key: jwt-secret
            - name: DatabaseSettings__ConnectionString
              valueFrom:
                secretKeyRef:
                  name: identity-secrets
                  key: db-connection
          volumeMounts:
            - name: config
              mountPath: /app/appsettings.Production.json
              subPath: appsettings.Production.json
      volumes:
        - name: config
          configMap:
            name: identity-config-{{ PROJECT_NAME }}
```

## 🔐 Environment Variables Override

You can override any configuration using environment variables:

### Project A (Docker)

```bash
docker run -d \
  -e MultiTenancy__Enabled=false \
  -e Jwt__Secret="projecta-secret" \
  -e Jwt__Issuer="ProjectA-IdentityService" \
  -e DatabaseSettings__ConnectionString="Host=db;Database=ProjectA_Identity" \
  identity-service:latest
```

### Project B (Docker)

```bash
docker run -d \
  -e MultiTenancy__Enabled=true \
  -e MultiTenancy__TenantServiceUrl="http://tenant-service:80" \
  -e Jwt__Secret="projectb-default-secret" \
  -e Jwt__Issuer="ProjectB-IdentityService" \
  -e DatabaseSettings__ConnectionString="Host=db;Database=ProjectB_Identity" \
  identity-service:latest
```

## 🎯 Unified Configuration Helper (Recommended Enhancement)

Create a helper class for consistent configuration access:

### Create: ConfigurationHelper.cs

```csharp
using Microsoft.Extensions.Configuration;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace Identity.Infrastructure.Helpers;

public static class ConfigurationHelper
{
    /// <summary>
    /// Gets configuration value with tenant fallback support
    /// </summary>
    public static string GetConfigValue(
        IConfiguration configuration,
        ITenantContext tenantContext,
        string configKey,
        Func<TenantConfiguration, string?>? tenantValueSelector = null)
    {
        // Try tenant config first if available
        if (tenantContext.HasTenant &&
            tenantContext.CurrentTenant?.Configuration != null &&
            tenantValueSelector != null)
        {
            var tenantValue = tenantValueSelector(tenantContext.CurrentTenant.Configuration);
            if (!string.IsNullOrEmpty(tenantValue))
            {
                return tenantValue;
            }
        }

        // Fallback to appsettings
        return configuration[configKey]
            ?? throw new InvalidOperationException($"Configuration key '{configKey}' not found");
    }

    /// <summary>
    /// Gets JWT settings with automatic tenant/default resolution
    /// </summary>
    public static JwtSettings GetJwtSettings(
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        // If tenant has custom JWT config, use it
        if (tenantContext.HasTenant &&
            tenantContext.CurrentTenant?.Configuration?.Jwt != null)
        {
            var tenantJwt = tenantContext.CurrentTenant.Configuration.Jwt;
            if (!string.IsNullOrEmpty(tenantJwt.Secret))
            {
                return new JwtSettings
                {
                    Secret = tenantJwt.Secret,
                    Issuer = tenantJwt.Issuer ?? configuration["Jwt:Issuer"]!,
                    Audience = tenantJwt.Audience ?? configuration["Jwt:Audience"]!,
                    AccessTokenExpirationMinutes = tenantJwt.AccessTokenExpirationMinutes > 0
                        ? tenantJwt.AccessTokenExpirationMinutes
                        : int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60")
                };
            }
        }

        // Fallback to appsettings
        return new JwtSettings
        {
            Secret = configuration["Jwt:Secret"]!,
            Issuer = configuration["Jwt:Issuer"]!,
            Audience = configuration["Jwt:Audience"]!,
            AccessTokenExpirationMinutes = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60")
        };
    }

    /// <summary>
    /// Gets database connection string with tenant support
    /// </summary>
    public static string GetDatabaseConnectionString(
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        return GetConfigValue(
            configuration,
            tenantContext,
            "DatabaseSettings:ConnectionString",
            tenant => tenant.Database?.ConnectionString
        );
    }
}

public record JwtSettings
{
    public required string Secret { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int AccessTokenExpirationMinutes { get; init; }
}
```

### Updated JwtTokenGenerator Using Helper

```csharp
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;

    public JwtTokenGenerator(IConfiguration configuration, ITenantContext tenantContext)
    {
        _configuration = configuration;
        _tenantContext = tenantContext;
    }

    public (string AccessToken, string RefreshToken, DateTime ExpiresAt) GenerateTokens(User user)
    {
        // ✨ Single line to get settings - handles tenant/default automatically!
        var jwtSettings = ConfigurationHelper.GetJwtSettings(_configuration, _tenantContext);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add tenant ID if available
        if (_tenantContext.HasTenant && _tenantContext.TenantId != null)
        {
            claims = claims.Append(new Claim("tenant_id", _tenantContext.TenantId)).ToArray();
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), GenerateRefreshToken(), expiresAt);
    }
}
```

## 📊 Comparison Table

| Aspect                   | Project A (No Tenants)   | Project B (With Tenants)              |
| ------------------------ | ------------------------ | ------------------------------------- |
| **Binary**               | ✅ Same                  | ✅ Same                               |
| **MultiTenancy:Enabled** | `false`                  | `true`                                |
| **JWT Source**           | appsettings.json only    | Tenant config or appsettings fallback |
| **Database**             | Single DB                | Single or per-tenant DB               |
| **Performance**          | ⚡ Fastest (no overhead) | ⚡ Fast (with caching)                |
| **x-tenant-id header**   | ❌ Ignored               | ✅ Required for tenant mode           |
| **Configuration**        | Static                   | Dynamic per tenant                    |
| **Deployment**           | Standard                 | Requires Tenant Service               |

## ✅ Deployment Checklist

### For Project A (No Tenants)

- [ ] Set `MultiTenancy:Enabled = false`
- [ ] Configure JWT settings in appsettings
- [ ] Configure database connection
- [ ] Configure CORS origins
- [ ] Deploy Identity Service only (no Tenant Service needed)
- [ ] Test without x-tenant-id header

### For Project B (With Tenants)

- [ ] Set `MultiTenancy:Enabled = true`
- [ ] Configure `MultiTenancy:TenantServiceUrl`
- [ ] Configure default JWT settings (fallback)
- [ ] Configure database connection
- [ ] Configure CORS origins
- [ ] Deploy both Tenant Service and Identity Service
- [ ] Create tenant configurations in Tenant Service
- [ ] Test with and without x-tenant-id header
- [ ] Verify cache behavior

## 🎓 Key Takeaways

1. ✅ **Single Binary**: Same compiled code works for both scenarios
2. ✅ **Configuration-Driven**: Behavior controlled by appsettings
3. ✅ **Automatic Fallback**: Always works even if tenant config fails
4. ✅ **Zero Overhead**: No performance impact when multi-tenancy is disabled
5. ✅ **Environment Variables**: Easy to override in containers/k8s
6. ✅ **Clean Architecture**: Same codebase, different deployments

## 🚀 Quick Start Commands

### Build Once

```bash
cd src/Services/Identity/Identity.API
dotnet publish -c Release -o ./publish
```

### Deploy to Project A

```bash
docker build -t identity-service:projecta .
docker run -d \
  -e MultiTenancy__Enabled=false \
  -e Jwt__Secret="projecta-secret" \
  -p 5001:80 \
  identity-service:projecta
```

### Deploy to Project B

```bash
docker build -t identity-service:projectb .
docker run -d \
  -e MultiTenancy__Enabled=true \
  -e MultiTenancy__TenantServiceUrl="http://tenant-service:80" \
  -e Jwt__Secret="projectb-default-secret" \
  -p 5001:80 \
  identity-service:projectb
```

---

**The beauty of this design**: You maintain a single codebase, single pipeline, and single binary that adapts to different deployment scenarios through configuration! 🎉
