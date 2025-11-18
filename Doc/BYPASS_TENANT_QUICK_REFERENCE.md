# 🚨 BypassTenant Endpoints - Quick Reference

**Quick fix guide for admin/global endpoints that work without tenant context**

---

## ⚠️ Critical Checklist (5 Steps)

Before creating admin endpoints with `BypassTenantAttribute`:

### 1️⃣ JwtMode Configuration (appsettings.json)

```json
{
  "MultiTenancy": {
    "JwtMode": "PerTenant" // ✅ MUST match Identity Service
  }
}
```

**❌ Problem**: Mismatched JwtMode → 401 Unauthorized for tenant users

---

### 2️⃣ JWT Validation Pattern (Program.cs)

```csharp
// ❌ WRONG - ITenantContext not populated yet
var tenantContext = context.HttpContext.RequestServices.GetService<ITenantContext>();

// ✅ CORRECT - Use ITenantConfigurationProvider directly
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var tenantId = context.HttpContext.Request.Headers["x-tenant-id"].FirstOrDefault();

        if (!string.IsNullOrEmpty(tenantId))
        {
            var provider = context.HttpContext.RequestServices
                .GetService<ITenantConfigurationProvider>();
            var tenant = provider.GetTenantConfigurationAsync(tenantId, ct)
                .GetAwaiter().GetResult();

            // Use tenant-specific JWT secret
            context.Options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(tenant.Configuration.Jwt.Secret)
                ),
                // ... other settings
            };
        }
        else
        {
            // ✅ CRITICAL: Always set global JWT params when no tenant
            context.Options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(globalSecret)
                ),
                // ... other settings
            };
        }

        return Task.CompletedTask;
    }
};
```

**❌ Problem**: Missing provider → Token validation fails

---

### 3️⃣ DbContext Fallback (YourDbContext.cs)

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    if (optionsBuilder.IsConfigured) return;

    var multiTenancyEnabled = _configuration?.GetValue<bool>("MultiTenancy:Enabled") ?? false;

    if (multiTenancyEnabled)
    {
        // ✅ CRITICAL - Fall back to global DB if no tenant context
        if (_tenantContext?.HasTenant != true ||
            _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
        {
            // Use global database from appsettings.json
            connectionString = _configuration["DatabaseSettings:ConnectionString"];
            provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";
        }
        else
        {
            // Use tenant-specific database
            connectionString = _tenantContext.CurrentTenant.Configuration
                .DatabaseSettings.ConnectionString;
            provider = _tenantContext.CurrentTenant.Configuration
                .DatabaseSettings.Provider ?? "PostgreSql";
        }
    }
    // Configure provider...
}
```

**❌ Problem**: No fallback → 400 Bad Request - Tenant context required

---

### 4️⃣ Dual Database Migration (Program.cs)

```csharp
// ✅ CORRECT - Run both migrations
app.UseDefaultDatabaseMigration<YourDbContext>(); // Global DB

if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<YourDbContext>(config); // Tenant DBs
}

// ❌ WRONG - Only one migration
if (multiTenancyEnabled)
{
    app.UseTenantDatabaseMigration<YourDbContext>(...); // ❌ Global DB never migrated!
}
```

**❌ Problem**: Missing global migration → 42P01: relation does not exist

---

### 5️⃣ Optional Tenant Context Endpoints

```csharp
// ✅ CORRECT - Optional tenantId
adminGroup.MapPost("/files", async (
    [FromQuery] string? tenantId, // ✅ Optional
    ITenantContext tenantContext,
    ITenantConfigurationProvider tenantConfigProvider,
    // ... other params
) =>
{
    // Only set tenant context if tenantId provided
    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        var tenantInfo = await tenantConfigProvider
            .GetTenantConfigurationAsync(tenantId, ct);

        if (tenantInfo == null)
            return Results.NotFound(new { error = "Tenant not found" });

        tenantContext.SetTenant(tenantInfo); // Manually set context
    }
    // else: No tenant context, uses global database

    // Handler executes with or without tenant context
})
.WithMetadata(new BypassTenantAttribute())
.RequireAuthorization(policy => policy.RequireRole("Service", "SuperAdmin"));

// ❌ WRONG - Required tenantId
adminGroup.MapPost("/files", async (
    [FromQuery] string tenantId, // ❌ Required
```

**❌ Problem**: Required parameter → Endpoint can't work without tenant

---

## 🚑 Quick Troubleshooting

| Symptom                                   | Root Cause                           | Fix           |
| ----------------------------------------- | ------------------------------------ | ------------- |
| 401 Unauthorized (tenant users)           | JwtMode mismatch                     | Check Step 1️⃣ |
| Token validation failed                   | Missing ITenantConfigurationProvider | Check Step 2️⃣ |
| 400 Bad Request - Tenant required         | No DbContext fallback                | Check Step 3️⃣ |
| 42P01: relation does not exist            | Missing global DB migration          | Check Step 4️⃣ |
| Admin can't use endpoint without tenantId | Required tenantId parameter          | Check Step 5️⃣ |

---

## 📖 Complete Implementation Guide

For detailed explanations, code examples, and testing strategies:

**[BYPASS_TENANT_ENDPOINTS_GUIDE.md](BYPASS_TENANT_ENDPOINTS_GUIDE.md)**

---

## 🎯 Usage Examples

### Upload to Specific Tenant

```http
POST /api/admin/files?tenantId=ihsandev
Authorization: Bearer <global-superadmin-jwt>
Content-Type: multipart/form-data
```

### Upload to Global Database

```http
POST /api/admin/files
Authorization: Bearer <global-superadmin-jwt>
Content-Type: multipart/form-data
```

---

**Last Updated**: November 2025
