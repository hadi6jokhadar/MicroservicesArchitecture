# 🚀 Multi-Tenancy Quick Start Guide

This guide will help you get the multi-tenancy feature up and running in under 10 minutes.

## ✅ Prerequisites

- .NET 9.0 SDK
- PostgreSQL (or your preferred database)
- Your favorite API testing tool (Postman, curl, etc.)

## 📝 Step-by-Step Setup

### 1. Start the Tenant Service

First, set up the Tenant Service database and start the service:

```bash
# Navigate to Tenant.API
cd src/Services/Tenant/Tenant.API

# Update connection string in appsettings.Development.json
# "ConnectionString": "Host=localhost;Port=5432;Database=TenantDb;Username=postgres;Password=YOUR_PASSWORD"

# Run migrations
dotnet ef migrations add InitialCreate --project ../Tenant.Infrastructure
dotnet ef database update

# Start the service (runs on https://localhost:5002)
dotnet run
```

### 2. Enable Multi-Tenancy in Identity Service

Update `src/Services/Identity/Identity.API/appsettings.json`:

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  }
}
```

### 3. Start the Identity Service

```bash
# Navigate to Identity.API
cd src/Services/Identity/Identity.API

# Start the service (runs on https://localhost:5001)
dotnet run
```

### 4. Create an Admin User

First, register a regular user, then manually promote them to Admin in the database:

```bash
curl -X POST "https://localhost:5001/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!@#",
    "firstName": "Admin",
    "lastName": "User"
  }'
```

Then update the database:

```sql
UPDATE "Users" SET "Role" = 'Admin' WHERE "Email" = 'admin@example.com';
```

### 5. Login as Admin

```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!@#"
  }'
```

Save the `accessToken` from the response.

### 6. Create a Tenant

```bash
curl -X POST "https://localhost:5002/api/admin/tenant" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN" \
  -d '{
    "tenantId": "company-abc",
    "tenantName": "ABC Corporation",
    "userId": 1,
    "startDate": "2025-01-01T00:00:00Z",
    "expireDate": "2026-01-01T00:00:00Z",
    "data": "{\"Jwt\":{\"Secret\":\"company-abc-secret-key-minimum-256-bits-long-for-security\",\"Issuer\":\"CompanyABC\",\"Audience\":\"CompanyABCApp\",\"AccessTokenExpirationMinutes\":60,\"RefreshTokenExpirationDays\":7}}"
  }'
```

### 7. Test Tenant-Specific Authentication

```bash
# Login WITH tenant header (uses tenant-specific JWT settings)
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "x-tenant-id: company-abc" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!@#"
  }'
```

The response token will:

- Be signed with the tenant-specific JWT secret
- Include a `tenant_id` claim
- Use tenant-specific issuer/audience

### 8. Test Default (Non-Tenant) Mode

```bash
# Login WITHOUT tenant header (uses appsettings.json)
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!@#"
  }'
```

This uses the default configuration from `appsettings.json`.

## ✨ Verify It's Working

### Check Tenant Configuration

```bash
curl -X GET "https://localhost:5002/api/tenant/config/company-abc"
```

You should see the tenant configuration including the settings data.

### Check Tenant Context in Logs

When you make a request with `x-tenant-id` header, check the Identity Service logs. You should see:

```
info: IhsanDev.Shared.Infrastructure.Middleware.TenantMiddleware[0]
      Tenant context set for tenant: company-abc (ABC Corporation)
```

## 🧪 Testing Scenarios

### Scenario 1: Multi-Tenancy Disabled (Default)

1. Set `MultiTenancy:Enabled = false` in Identity Service
2. Restart Identity Service
3. Send requests with or without `x-tenant-id` header
4. **Expected**: All requests use `appsettings.json` configuration

### Scenario 2: Valid Tenant

1. Send request with `x-tenant-id: company-abc`
2. **Expected**: Request succeeds with tenant-specific configuration

### Scenario 3: Invalid Tenant

1. Send request with `x-tenant-id: nonexistent-tenant`
2. **Expected**: 404 Not Found with error message

### Scenario 4: Inactive Tenant

1. Update tenant in database: `IsActive = false`
2. Send request with that tenant ID
3. **Expected**: 403 Forbidden

## 🔍 Troubleshooting

### Tenant Service Not Running

**Error**: `Error fetching tenant configuration`

**Solution**: Ensure Tenant Service is running on `https://localhost:5002`

### Database Connection Issues

**Error**: `Could not connect to database`

**Solution**: Check connection strings in `appsettings.Development.json`

### JWT Validation Failed

**Error**: `401 Unauthorized`

**Cause**: Token issued with different tenant configuration

**Solution**: Generate a new token with the correct tenant header

## 📚 Next Steps

- Read the full [Multi-Tenancy Guide](MULTI_TENANCY_GUIDE.md)
- Explore tenant management endpoints in Swagger UI
- Implement tenant-specific database connections
- Add tenant-specific CORS configuration

## 💡 Pro Tips

1. **Caching**: Tenant configs are cached for 5 minutes. Clear cache when updating:

   ```csharp
   _tenantConfigProvider.ClearCache("company-abc");
   ```

2. **Debugging**: Enable verbose logging:

   ```json
   {
     "Logging": {
       "LogLevel": {
         "IhsanDev.Shared.Infrastructure.Middleware.TenantMiddleware": "Debug"
       }
     }
   }
   ```

3. **Multiple Tenants**: Create different tenants with different configurations and test switching between them.

---

## Summary

You've successfully set up multi-tenancy! 🎉

- ✅ Tenant Service running
- ✅ Identity Service with multi-tenancy enabled
- ✅ Tenant created and configured
- ✅ Tenant-specific JWT working
- ✅ Backward compatibility maintained

Need help? Check the [main README](README.md) or the [Multi-Tenancy Guide](MULTI_TENANCY_GUIDE.md).
