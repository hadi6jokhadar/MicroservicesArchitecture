# 🏢 Multi-Tenancy Implementation Guide

## Overview

This microservices architecture supports **optional multi-tenancy** with **configurable JWT mode**, allowing each tenant (organization/customer) to have their own isolated configuration including database connections, JWT settings (optional), and CORS policies.

### Key Features

- ✅ **Optional Multi-Tenancy**: Easily enable/disable via configuration
- ✅ **Configurable JWT Mode**: Choose between Shared JWT (superadmin) or PerTenant JWT (isolation)
- ✅ **Per-Tenant Database**: Each tenant can have separate database
- ✅ **Per-Tenant Configuration**: Isolated Database, JWT (optional), and CORS settings
- ✅ **Tenant Service**: Dedicated microservice for tenant management
- ✅ **Configuration Caching**: Optimized performance with memory caching
- ✅ **Backward Compatible**: Zero breaking changes when disabled
- ✅ **Header-Based Resolution**: Tenant identified via `x-tenant-id` header

---

## Architecture

### Components

1. **Tenant Service** (`/src/Services/Tenant/`)

   - Manages tenant configurations
   - Stores tenant-specific settings in database
   - Provides API for configuration retrieval

2. **Shared Tenant Abstractions** (`/src/Shared/IhsanDev.Shared.Kernel/`)

   - `ITenantContext`: Access current tenant in request
   - `ITenantConfigurationProvider`: Fetch tenant configuration
   - `TenantInfo`: Tenant data model
   - `JwtMode`: Enum for JWT validation modes (Shared or PerTenant)

3. **Tenant Middleware** (`/src/Shared/IhsanDev.Shared.Infrastructure/`)

   - Extracts `x-tenant-id` from request headers
   - Resolves tenant configuration
   - Populates tenant context for the request

4. **Multi-Tenant Aware Services**
   - Database Context: Uses tenant-specific database connections
   - JWT Token Validator: Uses shared or tenant-specific JWT settings (configurable)
   - CORS Configuration: Tenant-specific allowed origins

---

## Configuration

### Enable Multi-Tenancy with Shared JWT (Recommended for Superadmin Access)

Update `appsettings.json` in services (e.g., Identity.API):

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "Shared",
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  },
  "Jwt": {
    "Secret": "your-shared-secret-key-minimum-32-characters",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "ExpiryInMinutes": 60
  }
}
```

**Note:** In Shared mode, all tenants use the JWT settings from the `Jwt` section in appsettings.json.

### Enable Multi-Tenancy with PerTenant JWT (Maximum Isolation)

Update `appsettings.json` in services (e.g., Identity.API):

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant",
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  }
}
```

**Note:** In PerTenant mode, each tenant must have their own JWT secret configured in the Tenant Service.

### Disable Multi-Tenancy (Default)

```json
{
  "MultiTenancy": {
    "Enabled": false
  }
}
```

When disabled, the system behaves exactly as before with no tenant resolution and uses static configuration from appsettings.json.

### JWT Mode Comparison

| JWT Mode      | Configuration                         | Use Case                           | Isolation                         |
| ------------- | ------------------------------------- | ---------------------------------- | --------------------------------- |
| **Shared**    | `"JwtMode": "Shared"` + `Jwt` section | Superadmin access, internal tools  | All tenants share JWT secret      |
| **PerTenant** | `"JwtMode": "PerTenant"`              | Enterprise customers, max security | Each tenant has unique JWT secret |

---

## Tenant Service Setup

### 1. Database Configuration

Update `appsettings.Development.json` in Tenant.API:

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=TenantDb;Username=postgres;Password=postgres"
  }
}
```

### 2. Run Migrations

```bash
cd src/Services/Tenant/Tenant.API
dotnet ef migrations add InitialCreate --project ../Tenant.Infrastructure
dotnet ef database update
```

### 3. Start Tenant Service

```bash
cd src/Services/Tenant/Tenant.API
dotnet run
```

The service will be available at:

- **HTTPS**: `https://localhost:5002`
- **Swagger UI**: `https://localhost:5002/swagger`

---

## Creating a Tenant

### API Request

**Endpoint**: `POST /api/admin/tenant`  
**Auth**: Requires Admin role  
**Headers**: `Authorization: Bearer {admin_token}`

```json
{
  "tenantId": "company-abc",
  "tenantName": "ABC Corporation",
  "userId": 1,
  "startDate": "2025-01-01T00:00:00Z",
  "expireDate": "2026-01-01T00:00:00Z",
  "data": "{\"Jwt\":{\"Secret\":\"tenant-specific-secret-key-min-256-bits\",\"Issuer\":\"CompanyABC\",\"Audience\":\"CompanyABCApp\",\"AccessTokenExpirationMinutes\":60,\"RefreshTokenExpirationDays\":7},\"Database\":{\"Provider\":\"PostgreSql\",\"ConnectionString\":\"Host=localhost;Database=CompanyABC_DB;...\"},\"Cors\":{\"AllowedOrigins\":[\"https://companyabc.com\"]}}"
}
```

### Tenant Configuration Structure

The `data` field contains a JSON string with tenant-specific settings:

```json
{
  "Jwt": {
    "Secret": "tenant-specific-secret-key-minimum-256-bits",
    "Issuer": "TenantIssuer",
    "Audience": "TenantAudience",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "Database": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=TenantDb;..."
  },
  "Cors": {
    "AllowedOrigins": ["https://tenant-app.com", "https://tenant-admin.com"]
  }
}
```

---

## Using Multi-Tenancy in Requests

### 1. Without Tenant (Default Behavior)

```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "Password123!"
  }'
```

Uses configuration from `appsettings.json`.

### 2. With Tenant (Multi-Tenant Mode)

```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "x-tenant-id: company-abc" \
  -d '{
    "email": "user@companyabc.com",
    "password": "Password123!"
  }'
```

Uses tenant-specific configuration from Tenant Service.

---

## How It Works

### Request Flow

```
1. Request arrives with x-tenant-id header
   ↓
2. Tenant Middleware extracts tenant ID
   ↓
3. Tenant Configuration Provider fetches config (with caching)
   ↓
4. Tenant Context is populated for the request
   ↓
5. Services (JWT, Database) use tenant-specific config
   ↓
6. Response sent with tenant-specific behavior
```

### Tenant Resolution Priority

1. **Tenant Header Present + Multi-Tenancy Enabled**

   - Fetch configuration from Tenant Service
   - Use tenant-specific settings
   - Add `tenant_id` claim to JWT tokens

2. **No Tenant Header OR Multi-Tenancy Disabled**
   - Use `appsettings.json` configuration
   - Behave as single-tenant application

---

## Tenant Management API

### Public Endpoints

| Method | Endpoint                        | Description                              |
| ------ | ------------------------------- | ---------------------------------------- |
| `GET`  | `/api/tenant/config/{tenantId}` | Get tenant configuration (includes Data) |
| `GET`  | `/api/tenant/{tenantId}`        | Get tenant info (excludes Data)          |

### Admin Endpoints (Requires Admin Role)

| Method   | Endpoint                          | Description                        |
| -------- | --------------------------------- | ---------------------------------- |
| `GET`    | `/api/admin/tenant`               | Get all active tenants (paginated) |
| `GET`    | `/api/admin/tenant/user/{userId}` | Get tenant by user ID              |
| `POST`   | `/api/admin/tenant`               | Create new tenant                  |
| `PUT`    | `/api/admin/tenant/{tenantId}`    | Update tenant settings             |
| `DELETE` | `/api/admin/tenant/{tenantId}`    | Delete tenant                      |

---

## Caching

Tenant configurations are cached in memory for performance:

- **Default Cache Duration**: 5 minutes
- **Configurable**: `MultiTenancy:CacheExpirationMinutes`
- **Cache Key**: `tenant_config_{tenantId}`

### Manual Cache Invalidation

```csharp
// Inject ITenantConfigurationProvider
private readonly ITenantConfigurationProvider _tenantConfigProvider;

// Clear specific tenant
_tenantConfigProvider.ClearCache("company-abc");

// Clear all tenants
_tenantConfigProvider.ClearAllCache();
```

---

## Code Examples

### Accessing Tenant Context in Your Code

```csharp
public class MyService
{
    private readonly ITenantContext _tenantContext;

    public MyService(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public void DoSomething()
    {
        if (_tenantContext.HasTenant)
        {
            var tenantId = _tenantContext.TenantId;
            var tenantName = _tenantContext.CurrentTenant?.TenantName;
            var jwtSecret = _tenantContext.CurrentTenant?.Configuration?.Jwt?.Secret;

            // Use tenant-specific logic
        }
        else
        {
            // Use default logic
        }
    }
}
```

### Tenant-Aware Database Context

```csharp
public class MyDbContext : BaseDbContext
{
    private readonly ITenantContext _tenantContext;

    public MyDbContext(
        DbContextOptions<MyDbContext> options,
        ICurrentUserService currentUserService,
        ITenantContext tenantContext) : base(options, currentUserService)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Use tenant-specific connection string if available
        if (_tenantContext.HasTenant &&
            _tenantContext.CurrentTenant?.Configuration?.Database?.ConnectionString != null)
        {
            var connectionString = _tenantContext.CurrentTenant.Configuration.Database.ConnectionString;
            optionsBuilder.UseNpgsql(connectionString);
        }

        base.OnConfiguring(optionsBuilder);
    }
}
```

---

## Security Considerations

### 1. Tenant Isolation

- Each tenant has isolated configuration
- Tenant ID is validated on every request
- Inactive or expired tenants are rejected

### 2. JWT Token Security

- Tenant-specific JWT secrets
- Tenant ID included in token claims
- Token validation uses tenant-specific keys

### 3. Configuration Storage

- Sensitive data (JWT secrets, DB passwords) stored encrypted
- Admin-only access to tenant management endpoints
- Audit logging for tenant changes

---

## Testing

### Test Multi-Tenancy Disabled (Default)

1. Ensure `MultiTenancy:Enabled = false`
2. Start Identity Service
3. Test login/registration
4. Verify normal operation

### Test Multi-Tenancy Enabled

1. Start Tenant Service
2. Create test tenant via API
3. Enable multi-tenancy in Identity Service
4. Send requests with `x-tenant-id` header
5. Verify tenant-specific JWT generation

### Test Cases

- ✅ Request without tenant header (should work)
- ✅ Request with valid tenant header (should use tenant config)
- ✅ Request with invalid tenant ID (should return 404)
- ✅ Request with inactive tenant (should return 403)
- ✅ JWT tokens contain `tenant_id` claim when tenant present
- ✅ Configuration caching works correctly

---

## Troubleshooting

### Tenant Not Found

```json
{
  "error": "Tenant not found or inactive",
  "tenantId": "company-abc"
}
```

**Solution**: Verify tenant exists and is active in Tenant Service.

### Configuration Fetch Failed

Check logs for:

```
Error fetching tenant configuration for 'tenant-id'
```

**Solutions**:

- Verify Tenant Service is running
- Check `MultiTenancy:TenantServiceUrl` configuration
- Verify network connectivity
- Check Tenant Service logs

### JWT Validation Failed

**Possible Causes**:

- Tenant-specific JWT secret mismatch
- Token issued with different tenant secret
- Tenant configuration not cached/fetched

**Solution**: Ensure token was generated with the same tenant configuration.

---

## Migration Path

### From Single-Tenant to Multi-Tenant

1. **Install Tenant Service**

   ```bash
   cd src/Services/Tenant/Tenant.API
   dotnet ef database update
   ```

2. **Create Tenants**

   - Import existing customers as tenants
   - Configure tenant-specific settings

3. **Enable Multi-Tenancy**

   - Update `appsettings.json` in services
   - Deploy changes

4. **Update Clients**
   - Add `x-tenant-id` header to API requests
   - Update authentication flows

---

## Performance Considerations

- **Caching**: Tenant configs cached for 5 minutes
- **Network Calls**: First request per tenant fetches config from Tenant Service
- **Memory Usage**: Each tenant config ~1-5 KB in cache
- **Overhead**: <5ms per request for tenant resolution (cached)

---

## Future Enhancements

- [ ] Distributed cache (Redis) for multi-instance deployments
- [ ] Tenant-specific database connections per request
- [ ] Tenant usage analytics and billing
- [ ] Automatic tenant provisioning
- [ ] Tenant-specific feature flags
- [ ] White-label UI support per tenant

---

## Summary

This multi-tenancy implementation provides:

- ✅ **Flexibility**: Enable/disable as needed
- ✅ **Isolation**: Per-tenant configuration
- ✅ **Performance**: Configuration caching
- ✅ **Security**: Tenant validation and JWT isolation
- ✅ **Maintainability**: Clean architecture with shared abstractions
- ✅ **Backward Compatibility**: No breaking changes when disabled

For questions or issues, refer to the main [README.md](../../README.md) or open an issue.
