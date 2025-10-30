# 🎯 Quick Reference Card

**One-Page Cheat Sheet for Microservices Architecture**

---

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│              SHARED SERVICES (Deploy ONCE)               │
│  ┌───────────────┐  ┌────────────────┐  ┌──────────┐   │
│  │ Identity      │  │ Tenant         │  │ File     │   │
│  │ (Port 5001)   │  │ (Port 5002)    │  │ Manager  │   │
│  └───────────────┘  └────────────────┘  └──────────┘   │
└─────────────────────────────────────────────────────────┘
            │                    │
    ┌───────┴────────────────────┴───────┐
    │                                    │
┌───▼──────────┐                  ┌─────▼──────────┐
│ Database 1   │                  │ Database 2     │
│ tenant_123   │                  │ tenant_456     │
│ (Acme Corp)  │                  │ (Widget Inc)   │
└──────────────┘                  └────────────────┘
```

**Key Concept:** Database-Per-Tenant Architecture

- Each tenant = Separate database
- Services route to different DBs dynamically
- Complete data isolation per tenant

---

## 🔑 Essential Concepts

| Concept       | Description       | Example                                              |
| ------------- | ----------------- | ---------------------------------------------------- |
| **TenantId**  | Database boundary | `tenant_123` → Database 1, `tenant_456` → Database 2 |
| **ProjectId** | Logical filter    | `ProjectA` and `ProjectB` in same database           |
| **UserId**    | User identity     | `john@acme.com` in tenant_123 DB                     |
| **JWT**       | Authentication    | Token signed by Identity Service                     |

---

## 🚀 Quick Commands

### **Start Services Locally**

```bash
# Identity Service
cd src/Services/Identity/Identity.API && dotnet run
# → https://localhost:5001

# Tenant Service
cd src/Services/Tenant/Tenant.API && dotnet run
# → https://localhost:5002

# Your Service
cd src/Services/YourService/YourService.API && dotnet run
```

### **Test Authentication**

```bash
# 1. Login
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Password123!"}'

# 2. Use token
curl https://localhost:5002/api/your-endpoint \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "x-tenant-id: tenant_123"
```

---

## 📝 Essential Configuration

### **JWT (All Services - MUST BE IDENTICAL)**

```json
{
  "Jwt": {
    "Secret": "your-secret-minimum-32-chars",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp"
  }
}
```

### **Multi-Tenancy (Optional per Service)**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  }
}
```

**⚠️ Important:** When `Enabled: true`, `x-tenant-id` header is **REQUIRED** for all requests. No fallback to appsettings.json.

**⚠️ Startup Configuration:** When multi-tenancy is enabled, skip database initialization at startup:

```csharp
// ✅ CORRECT - Skip if multi-tenancy enabled
if (app.Environment.IsDevelopment() && !builder.Configuration.GetValue<bool>("MultiTenancy:Enabled", false))
{
    await app.Services.InitializeDatabaseAsync<YourDbContext>(applyMigrations: true, seedData: true);
}
```

Tenant databases will be initialized automatically per-request.

### **OTP Configuration (Identity Service)**

```json
{
  "OtpSettings": {
    "CodeLength": 6,
    "ExpirationSeconds": 300,
    "MaxAttempts": 3,
    "LockoutMinutes": 15,
    "ResendCooldownSeconds": 60,
    "UseAlphanumeric": false,
    "SecretKey": ""
  }
}
```

**OTP Settings:**

| Setting                 | Default | Description                            |
| ----------------------- | ------- | -------------------------------------- |
| `CodeLength`            | 6       | Length of OTP code (4-10 recommended)  |
| `ExpirationSeconds`     | 300     | Code validity (5 minutes)              |
| `MaxAttempts`           | 3       | Failed attempts before lockout         |
| `LockoutMinutes`        | 15      | Lockout duration                       |
| `ResendCooldownSeconds` | 60      | Cooldown between code requests         |
| `UseAlphanumeric`       | false   | Alphanumeric (true) or numeric (false) |

**Multi-Tenant OTP:** Tenants can override these settings with custom OTP configuration (stored in Tenant Service). If tenant has no custom settings, falls back to appsettings.json.

---

## 🔧 Create New Service (3 Steps)

### **1. Authentication**

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(/* configure JWT */);

app.UseAuthentication();
app.UseAuthorization();
```

### **2. Multi-Tenancy (Optional)**

```csharp
// Program.cs
builder.Services.AddMultiTenancy(builder.Configuration);
// That's it! Middleware auto-registered
```

### **3. Access User & Tenant**

```csharp
// In your handler
var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

if (_tenantContext.HasTenant)
{
    var tenantId = _tenantContext.CurrentTenant.TenantId;
}
```

---

## 🧪 Testing

### **Generate Test Data**

```csharp
using IhsanDev.Shared.Testing.Helpers;

var userId = TenantTestHelper.GenerateUniqueUserId();
var tenantId = TenantTestHelper.GenerateUniqueTenantId("service-name");
```

### **Create Test Factory**

```csharp
public class CustomWebApplicationFactory :
    IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    // Override GetTestConfiguration() if needed
}
```

---

## 🎯 Common Patterns

### **Pattern 1: With Authentication Only**

```csharp
[Authorize]
public async Task<IResult> GetData()
{
    var userId = GetCurrentUserId();
    // No tenant filtering
}
```

### **Pattern 2: With Authentication + Multi-Tenancy**

```csharp
[Authorize]
public async Task<IResult> GetData()
{
    var userId = GetCurrentUserId();
    var tenantId = _tenantContext.CurrentTenant?.TenantId;

    // Filter by both user and tenant
}
```

### **Pattern 3: Multi-Tenant Data Access**

```csharp
// Middleware automatically connects to correct DB
var data = await _dbContext.Orders
    .Where(o => o.UserId == userId) // User filter
    .ToListAsync(); // Automatic tenant DB
```

---

## 📚 Quick Navigation

| Need                | Document                              |
| ------------------- | ------------------------------------- |
| **Getting Started** | `00_START_HERE.md`                    |
| **Architecture**    | `DATABASE_PER_TENANT_ARCHITECTURE.md` |
| **Authentication**  | `SHARED_IDENTITY_SERVICE_GUIDE.md`    |
| **New Service**     | `NEW_SERVICE_INTEGRATION_GUIDE.md`    |
| **Multi-Tenancy**   | `MULTI_TENANCY_GUIDE.md`              |
| **Quick Setup**     | `MULTI_TENANCY_QUICK_START.md`        |
| **File Storage**    | `FILE_MANAGER_SERVICE_GUIDE.md`       |
| **Caching**         | `CACHING_STRATEGY_COMPARISON.md`      |
| **Testing**         | `SHARED_TESTING_FILES.md`             |

---

## 🔍 Troubleshooting

| Issue                         | Solution                                                         |
| ----------------------------- | ---------------------------------------------------------------- |
| **401 Unauthorized**          | Check JWT secret matches Identity Service                        |
| **Tenant not found**          | Verify tenant exists: `GET /api/tenants/{id}`                    |
| **Connection error**          | Check Tenant Service URL in configuration                        |
| **Cache issues**              | Clear cache or restart service                                   |
| **Missing claims**            | Verify Identity Service includes claims in JWT                   |
| **OTP code invalid/expired**  | Codes expire after 5 minutes; request new code                   |
| **Account locked (OTP)**      | Too many failed attempts; wait 15 minutes or contact admin       |
| **OTP cooldown error**        | Wait 60 seconds between code requests                            |
| **OTP settings not working**  | Add `OtpSettings` section to appsettings.json                    |
| **Tenant OTP config ignored** | Enable multi-tenancy: `MultiTenancy:Enabled = true`              |
| **OTP migration missing**     | Run `dotnet ef database update` to apply UpdateOtpSecurityFields |

---

## ✅ Pre-Deployment Checklist

- [ ] JWT secrets in environment variables (not hardcoded)
- [ ] Database connections configured per tenant
- [ ] HTTPS enabled in production
- [ ] CORS configured for allowed origins
- [ ] Rate limiting on authentication endpoints
- [ ] Logging and monitoring configured
- [ ] Tenant isolation tested
- [ ] Backup strategy in place

---

## 🎓 Learning Path (1 Week)

**Day 1-2:** Architecture

- `00_START_HERE.md`
- `DATABASE_PER_TENANT_ARCHITECTURE.md`
- `SHARED_IDENTITY_SERVICE_GUIDE.md`

**Day 3-4:** Development

- `NEW_SERVICE_INTEGRATION_GUIDE.md`
- `MULTI_TENANCY_GUIDE.md`
- Create first service

**Day 5-7:** Advanced

- `CACHING_STRATEGY_COMPARISON.md`
- `SHARED_TESTING_FILES.md`
- Write tests for your service

---

## 📞 Quick Links

**Identity Service Endpoints:**

- POST `/api/auth/register` - Register user with password
- POST `/api/auth/login` - Login with email + password
- POST `/api/auth/register-with-code-by-phone` - Register with phone (passwordless)
- POST `/api/auth/register-with-code-by-email` - Register with email (passwordless)
- POST `/api/auth/get-verification-code-by-phone` - Request OTP code for phone
- POST `/api/auth/get-verification-code-by-email` - Request OTP code for email
- POST `/api/auth/login-with-code-by-phone` - Login with phone + OTP code
- POST `/api/auth/login-with-code-by-email` - Login with email + OTP code
- GET `/api/user/profile` - Get user info

**Tenant Service Endpoints:**

- GET `/api/tenants/{tenantId}` - Get tenant config
- POST `/api/tenants` - Create tenant (admin)
- GET `/api/tenants` - List active tenants

---

**Print this card and keep it handy! 📋**

_For complete documentation, see `Doc/00_START_HERE.md`_
