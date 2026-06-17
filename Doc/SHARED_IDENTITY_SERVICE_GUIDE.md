# 🔐 Shared Identity Service Guide

## Should You Use One Identity Service or Multiple?

**TL;DR: Use ONE shared Identity Service across all your projects (A, B, C, etc.). This is the industry-standard microservices pattern.**

---

## 📋 Table of Contents

1. [Quick Answer](#quick-answer)
2. [Why Single Identity Service?](#why-single-identity-service)
3. [Architecture Overview](#architecture-overview)
4. [How Your Current Setup Supports This](#how-your-current-setup-supports-this)
5. [Implementation Guide](#implementation-guide)
6. [Authentication Flow](#authentication-flow)
7. [When to Use Separate Identity Services](#when-to-use-separate-identity-services)
8. [**Tenant Service Architecture (NEW)**](#tenant-service-architecture)
9. [Security Best Practices](#security-best-practices)
10. [Troubleshooting](#troubleshooting)
11. [Summary & Action Plan](#summary--action-plan)

---

## Quick Answer

### ✅ **Recommended: Single Shared Identity Service**

```
┌─────────────────────────────────────────────────────────────┐
│                    SHARED IDENTITY SERVICE                   │
│                                                               │
│  • User Database (PostgreSQL)                                │
│  • JWT Token Generation                                      │
│  • User Management (Register, Login, Profile)                │
│  • Role Management (User, Admin)                             │
└────────────────────┬──────────────────┬─────────────────────┘
                     │                  │
         ┌───────────▼──────┐  ┌────────▼───────────┐
         │   PROJECT A      │  │   PROJECT B        │
         │                  │  │                    │
         │  • Validates JWT │  │  • Validates JWT   │
         │  • Gets User ID  │  │  • Gets User ID    │
         │  • Uses Claims   │  │  • Uses Claims     │
         └──────────────────┘  └────────────────────┘
```

### ❌ **NOT Recommended: Separate Identity Services**

```
┌──────────────────┐              ┌──────────────────┐
│  Identity A      │              │  Identity B      │
│  • User DB A     │              │  • User DB B     │
│  • JWT Tokens A  │              │  • JWT Tokens B  │
└────────┬─────────┘              └─────────┬────────┘
         │                                  │
         │                                  │
    ┌────▼──────┐                      ┌────▼──────┐
    │ Project A │                      │ Project B │
    │           │                      │           │
    └───────────┘                      └───────────┘

    ❌ User has 2 accounts
    ❌ Must login twice
    ❌ Data inconsistency
    ❌ Double maintenance cost
```

---

## Why Single Identity Service?

### ✅ **Advantages of Shared Identity Service**

#### **1. Single Source of Truth**

- **One user database** - No duplicate or conflicting user data
- **Consistent credentials** - User's email, password, profile are centralized
- **Unified user management** - Add/remove users in one place

#### **2. Superior User Experience**

- **Single Sign-On (SSO)** - Login once, access all applications
- **Seamless navigation** - Users move between Project A ↔ Project B without re-login
- **One account to remember** - Reduces password fatigue

#### **3. Cost Effective**

- **1 database** instead of N databases (N = number of projects)
- **1 service to host** instead of N services
- **Lower infrastructure costs** - Single server, single backup strategy

#### **4. Simplified Management**

- **Centralized user administration** - Manage all users from one dashboard
- **Unified role/permission system** - Assign roles once, apply everywhere
- **Easier auditing** - One audit log for all authentication events

#### **5. Security Benefits**

- **Centralized security updates** - Patch once, all projects protected
- **Consistent security policies** - Password rules, MFA, lockout policies apply everywhere
- **Reduced attack surface** - One authentication endpoint to secure

#### **6. Developer Productivity**

- **Shared authentication code** - Write JWT validation once, reuse everywhere
- **Consistent API patterns** - Same login flow across all projects
- **Easier testing** - One authentication system to test

### ❌ **Disadvantages of Multiple Identity Services**

| Problem                  | Impact                                                          | Example                                                 |
| ------------------------ | --------------------------------------------------------------- | ------------------------------------------------------- |
| **Duplicate Users**      | Users need separate accounts for each project                   | John has `john@email.com` in both Identity A and B      |
| **Data Inconsistency**   | User updates profile in Project A, but Project B has stale data | John changes email in A, but B still has old email      |
| **Poor UX**              | Users must login separately to each application                 | Login to Project A, then login again to Project B       |
| **Authentication Drift** | Different password policies, MFA settings across projects       | Project A requires 12-char passwords, B requires 8-char |
| **Higher Costs**         | N databases + N services to host and maintain                   | 2 projects = 2× database costs, 2× server costs         |
| **Security Nightmare**   | Security patches must be applied N times                        | CVE fix required in 3 separate codebases                |
| **Maintenance Burden**   | Bug fixes must be duplicated across all services                | Fix login bug in Project A, must also fix in B, C, D... |
| **No SSO**               | Users can't seamlessly move between applications                | Log out of Project A, must login again for Project B    |
| **Audit Complexity**     | User activity scattered across N systems                        | Where did user login from? Must check N audit logs      |

---

## Architecture Overview

### **Microservices Authentication Pattern**

```
┌─────────────────────────────────────────────────────────────────────┐
│                         IDENTITY SERVICE                             │
│                         (Port 5001)                                  │
│                                                                       │
│  Endpoints:                                                          │
│  • POST /api/v1/auth/register   - Create new user                   │
│  • POST /api/v1/auth/login      - Get JWT token                     │
│  • POST /api/v1/auth/refresh    - Refresh expired token             │
│  • GET  /api/v1/user/profile    - Get current user info             │
│  • PUT  /api/v1/user/profile    - Update user info                  │
│                                                                       │
│  Database: PostgreSQL (User accounts, roles, refresh tokens)        │
└───────────────────────────┬─────────────────────────────────────────┘
                            │
                            │ JWT Token
                            │ (Contains: UserId, Email, Roles)
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        ▼                   ▼                   ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│  PROJECT A    │   │  PROJECT B    │   │  PROJECT C    │
│  (Port 5003)  │   │  (Port 5004)  │   │  (Port 5005)  │
│               │   │               │   │               │
│  • Orders     │   │  • Inventory  │   │  • Shipping   │
│  • Products   │   │  • Warehouse  │   │  • Tracking   │
│               │   │               │   │               │
│  Validates:   │   │  Validates:   │   │  Validates:   │
│  • JWT Token  │   │  • JWT Token  │   │  • JWT Token  │
│  • User ID    │   │  • User ID    │   │  • User ID    │
│  • Roles      │   │  • Roles      │   │  • Roles      │
└───────────────┘   └───────────────┘   └───────────────┘
```

### **Key Components**

#### **Identity Service Responsibilities**

- ✅ User registration and login
- ✅ JWT token generation and validation
- ✅ Password hashing and verification
- ✅ Role and permission management
- ✅ Refresh token rotation
- ✅ User profile management

#### **Project A/B/C Responsibilities**

- ✅ Validate JWT token (using same secret key)
- ✅ Extract user information from token claims
- ✅ Enforce authorization rules (roles, policies)
- ✅ Business logic specific to that project

#### **What Projects DON'T Do**

- ❌ Store user credentials (passwords)
- ❌ Generate JWT tokens
- ❌ Manage user registration/login
- ❌ Handle password resets

---

## How Your Current Setup Supports This

### **Your Identity Service Configuration**

Looking at your `Identity.API/appsettings.json`:

```json
{
  "Jwt": {
    "Secret": "CHANGE_ME_JWT_SECRET",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp", // ← Generic audience (not project-specific)
    "AccessTokenExpirationMinutes": 21600,
    "RefreshTokenExpirationDays": 7
  }
}
```

### **Why This Configuration is Perfect for Shared Use**

#### **1. Generic Audience**

```json
"Audience": "MicroservicesApp"
```

- ✅ **NOT** project-specific (e.g., `"ProjectA"` or `"ProjectB"`)
- ✅ **Generic** name covers all microservices
- ✅ **All projects validate** against same audience

#### **2. Centralized Issuer**

```json
"Issuer": "IhsanDev"
```

- ✅ Single issuer for all tokens
- ✅ All projects trust tokens from `"IhsanDev"`

#### **3. Shared Secret Key**

```json
"Secret": "CHANGE_ME_JWT_SECRET"
```

- ✅ All projects use the **same secret** to validate JWT signatures
- ✅ Tokens signed by Identity Service can be validated by any project

### **JWT Token Structure**

When a user logs in, the Identity Service generates a JWT token like this:

```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "sub": "12345", // User ID
    "email": "john@example.com", // User email
    "name": "John Doe", // User name
    "role": "User", // User role
    "iss": "IdentityService", // Issuer
    "aud": "MicroservicesApp", // Audience
    "exp": 1729360800, // Expiration (Unix timestamp)
    "iat": 1729357200 // Issued at
  },
  "signature": "..." // HMAC signature using secret key
}
```

**All projects (A, B, C) can decode this token to get:**

- User ID: `12345`
- Email: `john@example.com`
- Role: `User`

---

## Implementation Guide

### **Step 1: Deploy Identity Service (Once)**

#### **Option A: Local Development**

```bash
# Run Identity Service locally
cd src/Services/Identity/Identity.API
dotnet run
# Identity Service running at: https://localhost:5001
```

#### **Option B: Production Deployment (Azure)**

```bash
# Deploy to Azure App Service
az webapp create --resource-group MyResourceGroup --plan MyAppServicePlan --name identity-api-prod
az webapp deployment source config-zip --resource-group MyResourceGroup --name identity-api-prod --src publish.zip

# Identity Service URL: https://identity-api-prod.azurewebsites.net
```

#### **Option C: Docker Container**

```bash
# Build and run Identity Service in Docker
docker build -t identity-service:latest .
docker run -d -p 5001:8080 --name identity-api identity-service:latest

# Identity Service running at: http://localhost:5001
```

---

### **Step 2: Configure Project A**

#### **Project A - `appsettings.json`**

```json
{
  "Jwt": {
    "Secret": "CHANGE_ME_JWT_SECRET",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp"
  },

  "IdentityServiceUrl": "https://identity-api-prod.azurewebsites.net"
}
```

#### **Project A - `Program.cs`**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero, // No tolerance for expired tokens

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"Token validated for user: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Add authentication middleware (ORDER MATTERS!)
app.UseAuthentication();  // Must come before UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.Run();
```

#### **Project A - Controller Example**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ProjectA.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpGet]
    [Authorize] // Requires valid JWT token
    public IActionResult GetOrders()
    {
        // Get user ID from JWT token claims
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            Message = "Orders for user",
            UserId = userId,
            Email = email,
            Role = role
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")] // Only Admin role can access
    public IActionResult CreateOrder()
    {
        // Only users with "Admin" role can reach here
        return Ok("Order created");
    }
}
```

---

### **Step 3: Configure Project B (Same Configuration)**

#### **Project B - `appsettings.json`**

```json
{
  "Jwt": {
    "Secret": "CHANGE_ME_JWT_SECRET", // ← Same secret as Project A
    "Issuer": "IhsanDev", // ← Same issuer
    "Audience": "MicroservicesApp" // ← Same audience
  },

  "IdentityServiceUrl": "https://identity-api-prod.azurewebsites.net" // ← Same Identity Service
}
```

#### **Project B - `Program.cs`**

```csharp
// EXACT SAME authentication configuration as Project A
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });

app.UseAuthentication();
app.UseAuthorization();
```

#### **Project B - Controller Example**

```csharp
namespace ProjectB.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    [HttpGet]
    [Authorize] // Same JWT token from Project A works here!
    public IActionResult GetInventory()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Ok(new
        {
            Message = "Inventory for user",
            UserId = userId
        });
    }
}
```

---

### **Step 4: Configure Additional Projects (C, D, E...)**

For each new project, **repeat the exact same configuration**:

1. ✅ Add same `Jwt` configuration to `appsettings.json`
2. ✅ Add same `AddAuthentication()` code to `Program.cs`
3. ✅ Add `[Authorize]` attribute to protected endpoints

**No changes needed to Identity Service!**

---

## Authentication Flow

### **Flow 1: User Login (Get JWT Token)**

```
┌──────────┐                ┌──────────────────┐
│  User    │                │ Identity Service │
└────┬─────┘                └────────┬─────────┘
     │                               │
     │ 1. POST /api/auth/login       │
     │ Content-Type: application/json│
     │ {                             │
     │   "email": "john@example.com",│
     │   "password": "SecurePass123!"│
     │ }                             │
     │──────────────────────────────▶│
     │                               │
     │                               │ 2. Validate credentials
     │                               │    (check password hash)
     │                               │
     │                               │ 3. Generate JWT token
     │                               │    (sign with secret key)
     │                               │
     │ 4. 200 OK                     │
     │ {                             │
     │   "accessToken": "eyJhbGc...",│
     │   "refreshToken": "...",      │
     │   "expiresIn": 3600           │
     │ }                             │
     │◀──────────────────────────────│
     │                               │
```

### **Flow 2: Access Project A with JWT**

```
┌──────────┐                ┌───────────┐
│  User    │                │ Project A │
└────┬─────┘                └─────┬─────┘
     │                            │
     │ 1. GET /api/orders         │
     │ Authorization: Bearer eyJhbGc...
     │───────────────────────────▶│
     │                            │
     │                            │ 2. Validate JWT:
     │                            │    - Check signature (using Jwt:Secret)
     │                            │    - Check issuer = "IhsanDev"
     │                            │    - Check audience = "MicroservicesApp"
     │                            │    - Check expiration
     │                            │
     │                            │ 3. Extract claims:
     │                            │    - UserId = "12345"
     │                            │    - Email = "john@example.com"
     │                            │    - Role = "User"
     │                            │
     │                            │ 4. Execute business logic
     │                            │    (fetch orders for user 12345)
     │                            │
     │ 5. 200 OK                  │
     │ { orders: [...] }          │
     │◀───────────────────────────│
     │                            │
```

### **Flow 3: Access Project B with Same JWT**

```
┌──────────┐                ┌───────────┐
│  User    │                │ Project B │
└────┬─────┘                └─────┬─────┘
     │                            │
     │ 1. GET /api/inventory      │
     │ Authorization: Bearer eyJhbGc... (SAME token from Flow 2!)
     │───────────────────────────▶│
     │                            │
     │                            │ 2. Validate JWT (same process)
     │                            │ 3. Extract claims (same user ID)
     │                            │ 4. Execute business logic
     │                            │
     │ 5. 200 OK                  │
     │ { inventory: [...] }       │
     │◀───────────────────────────│
     │                            │
```

**Key Point:** The **same JWT token** works for Project A, B, C, etc. because they all use the same validation parameters.

---

## When to Use Separate Identity Services

### **❌ Generally NOT Recommended**

In **99% of cases**, a single shared Identity Service is the correct choice.

### **✅ Only Consider Separate Identity Services If:**

#### **Scenario 1: Complete Isolation Required (Regulatory Compliance)**

**Example:**

- **Project A:** Banking/Financial application (PCI-DSS compliance)
- **Project B:** Internal HR system (GDPR compliance)

**Reason:**

- Regulatory requirements mandate **separate user databases**
- Audit requirements prevent user data sharing
- Different data residency requirements (EU vs US)

**Trade-off:**

- ✅ Meets compliance requirements
- ❌ Very high complexity
- ❌ Users need 2 accounts
- ❌ Requires federation/SSO protocols to work together

#### **Scenario 2: Different Authentication Providers**

**Example:**

- **Project A:** Public-facing app (OAuth 2.0 / Google / Facebook login)
- **Project B:** Internal corporate app (Active Directory / LDAP)

**Reason:**

- Different authentication mechanisms
- Different user identity stores

**Trade-off:**

- ✅ Each system uses native auth provider
- ❌ Requires federation (SAML / OAuth) to bridge systems
- ❌ Complex token exchange protocols

#### **Scenario 3: Acquired/Legacy Systems**

**Example:**

- **Project A:** Your new microservices app
- **Project B:** Acquired company's legacy monolith

**Reason:**

- Legacy system has existing users and auth system
- Migration risk too high in short term

**Solution:**

- ✅ **Short term:** Keep separate auth systems
- ✅ **Long term:** Migrate legacy users to shared Identity Service
- ✅ **Interim:** Use federation (SSO) to bridge systems

---

## Tenant Service Architecture

### **Quick Answer: Tenant Service Deployment**

**✅ RECOMMENDED: Host ONE Shared Tenant Service (Same as Identity Service)**

Just like Identity Service, you should **deploy ONE Tenant Service** that all projects (A, B, C) connect to.

### **Architecture Comparison**

#### **✅ Recommended: Shared Tenant Service**

```
┌─────────────────────────────────────────────────────────────┐
│                    SHARED TENANT SERVICE                     │
│                         (Port 5002)                          │
│                                                               │
│  • Tenant Database (PostgreSQL)                              │
│  • Tenant Configuration (Settings, Features)                │
│  • Tenant Management (Create, Update, Enable/Disable)       │
│  • Subscription Plans & Limits                               │
└────────────────────┬──────────────────┬─────────────────────┘
                     │                  │
         ┌───────────▼──────┐  ┌────────▼───────────┐
         │   PROJECT A      │  │   PROJECT B        │
         │                  │  │                    │
         │  • Calls API     │  │  • Calls API       │
         │  • Caches Config │  │  • Caches Config   │
         │  • Filter Data   │  │  • Filter Data     │
         └──────────────────┘  └────────────────────┘
```

**Benefits:**

- ✅ Single source of truth for all tenant data
- ✅ Centralized tenant management
- ✅ Shared tenant configuration across projects
- ✅ Unified billing and subscription management
- ✅ Lower infrastructure costs

#### **❌ NOT Recommended: Separate Tenant Services**

```
┌──────────────────┐              ┌──────────────────┐
│  Tenant Service  │              │  Tenant Service  │
│  for Project A   │              │  for Project B   │
│                  │              │                  │
│  • Tenant DB A   │              │  • Tenant DB B   │
└────────┬─────────┘              └─────────┬────────┘
         │                                  │
    ┌────▼──────┐                      ┌────▼──────┐
    │ Project A │                      │ Project B │
    └───────────┘                      └───────────┘

❌ Tenant data inconsistency
❌ Different tenant settings per project
❌ Cannot share tenants across projects
❌ Double maintenance burden
```

---

### **Why Shared Tenant Service?**

#### **Scenario: Multi-Project SaaS Platform**

Imagine you're building a SaaS platform with multiple products:

- **Project A:** CRM System
- **Project B:** Inventory Management
- **Project C:** Reporting Dashboard

**Customer "Acme Corp" (Tenant ID: 123) should:**

- ✅ Have ONE subscription across all products
- ✅ Have SAME settings (branding, features) everywhere
- ✅ Be billed ONCE for all products
- ✅ Be enabled/disabled across ALL products at once

**With Shared Tenant Service:**

```
Tenant ID: 123 (Acme Corp)
├─ Subscription: Enterprise Plan
├─ Status: Enabled
├─ Settings: { logo: "acme.png", primaryColor: "#FF5733" }
├─ Features: { analytics: true, apiAccess: true }
└─ Used By: Project A, B, C (all projects see same data)
```

**With Separate Tenant Services (WRONG):**

```
Tenant Service A:
  Tenant ID: 123 → Subscription: Enterprise, Status: Enabled

Tenant Service B:
  Tenant ID: 123 → Subscription: Basic (?), Status: Disabled (?)

❌ Data inconsistency!
❌ Customer confused about their subscription
❌ Billing nightmare (which service to charge?)
```

---

### **Key Differences: Identity vs Tenant Service**

| Aspect            | Identity Service                           | Tenant Service                                          |
| ----------------- | ------------------------------------------ | ------------------------------------------------------- |
| **Purpose**       | User authentication & authorization        | Tenant configuration & management                       |
| **Database**      | Users, roles, passwords, refresh tokens    | Tenants, settings, features, subscriptions              |
| **Used By**       | ALL projects (required for auth)           | Projects with multi-tenancy enabled (optional)          |
| **Deployment**    | ONE shared service                         | ONE shared service                                      |
| **Caching**       | Not typically cached (JWT is stateless)    | Cached with 5-minute TTL (IMemoryCache)                 |
| **API Calls**     | Only during login/registration             | During request processing (if multi-tenancy enabled)    |
| **Configuration** | `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience` | `MultiTenancy:TenantServiceUrl`, `MultiTenancy:Enabled` |

---

### **How Your Projects Use Tenant Service**

#### **Your Current Multi-Tenancy Architecture**

```
┌──────────────────────────────────────────────────────────────────┐
│                        TENANT SERVICE                             │
│                         (Port 5002)                               │
│                                                                    │
│  Endpoints:                                                       │
│  • GET  /api/v1/tenant/{tenantId}      - Get tenant configuration │
│  • POST /api/v1/admin/tenant           - Create new tenant        │
│  • PUT  /api/v1/admin/tenant/{id}      - Update tenant settings   │
│  • DELETE /api/v1/admin/tenant/{id}    - Disable tenant           │
│                                                                    │
│  Database: tenant (PostgreSQL)                                    │
└───────────────────────────┬──────────────────────────────────────┘
                            │
                            │ HTTP Request (with caching)
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        ▼                   ▼                   ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│  PROJECT A    │   │  PROJECT B    │   │  PROJECT C    │
│               │   │               │   │               │
│  appsettings: │   │  appsettings: │   │  appsettings: │
│  "Enabled":   │   │  "Enabled":   │   │  "Enabled":   │
│     true      │   │     true      │   │     false     │
│               │   │               │   │               │
│  Uses:        │   │  Uses:        │   │  Uses:        │
│  • Shared lib │   │  • Shared lib │   │  • NO tenant  │
│  • Auto cache │   │  • Auto cache │   │  • filtering  │
│  • Middleware │   │  • Middleware │   │               │
└───────────────┘   └───────────────┘   └───────────────┘
```

#### **Configuration in Each Project**

**Projects that DON'T need multi-tenancy (e.g., Project C):**

```json
{
  "MultiTenancy": {
    "Enabled": false
  }
}
```

**Projects that NEED multi-tenancy (e.g., Projects A & B):**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://tenant-service-prod.azurewebsites.net",
    "CacheExpirationMinutes": 5
  }
}
```

**Key Point:** Both Project A and B point to the **SAME Tenant Service URL**.

---

### **How Multi-Tenancy Works Automatically**

#### **1. Shared Library Handles Everything**

Your projects use `IhsanDev.Shared.Infrastructure` which includes:

```csharp
// In Program.cs of Project A/B
builder.Services.AddMultiTenancy(builder.Configuration);

app.UseTenantResolution(); // Adds middleware automatically
```

#### **2. What Happens Per Request**

```
┌──────────┐                ┌───────────┐                ┌────────────────┐
│  Client  │                │ Project A │                │ Tenant Service │
└────┬─────┘                └─────┬─────┘                └────────┬───────┘
     │                            │                               │
     │ 1. GET /api/orders         │                               │
     │ x-tenant-id: 123           │                               │
     │───────────────────────────▶│                               │
     │                            │                               │
     │                            │ 2. Middleware extracts        │
     │                            │    tenant ID from header      │
     │                            │                               │
     │                            │ 3. Check cache for tenant 123 │
     │                            │    (5-minute TTL)             │
     │                            │                               │
     │                            │ 4. Cache MISS                 │
     │                            │                               │
     │                            │ 5. GET /api/tenants/123       │
     │                            │──────────────────────────────▶│
     │                            │                               │
     │                            │ 6. Tenant config              │
     │                            │    { id: 123, enabled: true } │
     │                            │◀──────────────────────────────│
     │                            │                               │
     │                            │ 7. Store in cache (5 min)     │
     │                            │                               │
     │                            │ 8. Set ITenantContext         │
     │                            │    _tenantContext.TenantId=123│
     │                            │                               │
     │                            │ 9. Execute business logic     │
     │                            │    (filter orders by tenant)  │
     │                            │                               │
     │ 10. Orders for tenant 123  │                               │
     │◀───────────────────────────│                               │
     │                            │                               │
```

**Next request from same tenant (within 5 minutes):**

```
     │ 11. GET /api/products      │                               │
     │ x-tenant-id: 123           │                               │
     │───────────────────────────▶│                               │
     │                            │                               │
     │                            │ 12. Check cache → CACHE HIT   │
     │                            │     (no API call needed)      │
     │                            │                               │
     │                            │ 13. Use cached config         │
     │                            │                               │
     │ 14. Products for tenant 123│                               │
     │◀───────────────────────────│                               │
```

---

### **Deployment Strategy**

#### **Option 1: Single Deployment for ALL Projects (RECOMMENDED)**

```
Production Environment:
┌────────────────────────────────────────────────┐
│  Shared Services (Deploy ONCE)                 │
│  ├─ Identity Service (identity-api.azure.com)  │
│  └─ Tenant Service (tenant-api.azure.com)      │
└────────────────────────────────────────────────┘

┌────────────────────────────────────────────────┐
│  Project A (projecta-api.azure.com)            │
│  Points to: tenant-api.azure.com               │
└────────────────────────────────────────────────┘

┌────────────────────────────────────────────────┐
│  Project B (projectb-api.azure.com)            │
│  Points to: tenant-api.azure.com (SAME)        │
└────────────────────────────────────────────────┘
```

**Configuration:**

```bash
# Deploy Tenant Service ONCE
az webapp create --name tenant-api-prod --resource-group MyRG

# Configure Project A to use it
az webapp config appsettings set --name projecta-api-prod \
  --settings MultiTenancy__TenantServiceUrl="https://tenant-api-prod.azurewebsites.net"

# Configure Project B to use SAME Tenant Service
az webapp config appsettings set --name projectb-api-prod \
  --settings MultiTenancy__TenantServiceUrl="https://tenant-api-prod.azurewebsites.net"
```

#### **Option 2: Per-Project Tenant Services (NOT RECOMMENDED)**

**Only consider this if:**

- Projects have **completely different** tenant models (different database schemas)
- Regulatory compliance requires **data isolation** between projects
- Projects are owned by **different companies/departments**

**Example:**

```
Project A: SaaS CRM (tenants = companies)
Project B: Healthcare App (tenants = hospitals, HIPAA compliance)

→ Different tenant models, different compliance requirements
→ Separate Tenant Services might be justified
```

**But even then, consider:**

- Can you use a **single Tenant Service with different tenant types**?
- Can you achieve data isolation through **row-level security** instead?

---

### **When Multi-Tenancy is OPTIONAL**

#### **Projects that DON'T Need Tenant Service**

Some projects in your architecture might not need multi-tenancy at all:

**Example 1: Notification Service**

```json
{
  "MultiTenancy": {
    "Enabled": false // ← Just sends emails, no tenant filtering
  }
}
```

**Example 2: Logging/Monitoring Service**

```json
{
  "MultiTenancy": {
    "Enabled": false // ← Aggregates logs from all tenants
  }
}
```

**Example 3: Internal Admin Dashboard**

```json
{
  "MultiTenancy": {
    "Enabled": false // ← Manages all tenants, not scoped to one
  }
}
```

**These services:**

- ❌ Don't need `TenantServiceUrl` configuration
- ❌ Don't call Tenant Service API
- ❌ Don't filter data by tenant
- ✅ Work independently of tenant context

---

### **Caching Strategy: Why Projects Cache Tenant Config**

#### **Problem: API Calls on Every Request**

**Without caching:**

```
User makes 10 requests → Project makes 10 API calls to Tenant Service
10,000 users → 100,000 API calls per second!
```

#### **Solution: 5-Minute Cache (IMemoryCache)**

**Your current implementation (in shared library):**

```csharp
// TenantConfigurationProvider.cs
public async Task<TenantConfiguration?> GetTenantConfigurationAsync(string tenantId)
{
    var cacheKey = $"tenant_config_{tenantId}";

    // Check cache first
    if (_cache.TryGetValue(cacheKey, out TenantConfiguration? cachedConfig))
    {
        return cachedConfig;  // Cache hit - 0.001ms response time
    }

    // Cache miss - call Tenant Service API
    var config = await _httpClient.GetFromJsonAsync<TenantConfiguration>($"/api/v1/tenant/{tenantId}");

    // Store in cache for 5 minutes
    _cache.Set(cacheKey, config, TimeSpan.FromMinutes(5));

    return config;
}
```

**Performance impact:**

- **Cache HIT:** 0.001ms (instant, in-process memory)
- **Cache MISS:** 50-200ms (HTTP call to Tenant Service)
- **Cache duration:** 5 minutes (configurable)

**With caching:**

```
10,000 users, 5-minute cache:
→ ~100 API calls per minute to Tenant Service (manageable)
→ 99.9% of requests served from cache (0.001ms)
```

**Important:** If you scale to multiple instances of Project A, consider migrating from IMemoryCache to Redis (see CACHING_STRATEGY_COMPARISON.md).

---

### **Configuration Checklist**

#### **For Tenant Service (Deploy ONCE)**

- [ ] Deploy Tenant Service to production
- [ ] Configure PostgreSQL database
- [ ] Secure with JWT authentication (same as Identity Service)
- [ ] Set up CORS for allowed origins
- [ ] Test endpoints: GET /api/tenants/{id}

#### **For Each Project That Needs Multi-Tenancy**

- [ ] Set `MultiTenancy:Enabled = true` in appsettings.json
- [ ] Set `MultiTenancy:TenantServiceUrl` to shared Tenant Service URL
- [ ] Add `builder.Services.AddMultiTenancy(configuration)` in Program.cs
- [ ] Add `app.UseTenantResolution()` before authentication middleware
- [ ] Inject `ITenantContext` in services that need tenant filtering
- [ ] Test with `x-tenant-id` header in requests

#### **For Projects That DON'T Need Multi-Tenancy**

- [ ] Set `MultiTenancy:Enabled = false` in appsettings.json
- [ ] No other configuration needed

---

### **Summary: Tenant Service Deployment**

| Aspect                | Recommendation                                | Reason                                 |
| --------------------- | --------------------------------------------- | -------------------------------------- |
| **Deployment**        | ONE shared Tenant Service                     | Single source of truth, lower cost     |
| **URL Configuration** | ALL projects point to SAME URL                | Shared tenant data across projects     |
| **Enablement**        | Per-project via `MultiTenancy:Enabled`        | Some services don't need multi-tenancy |
| **Caching**           | 5-minute IMemoryCache (already implemented)   | Reduces API calls, faster responses    |
| **Scaling**           | Migrate to Redis when scaling to 2+ instances | Maintains cache consistency            |

**Your architecture already follows best practices!** Just deploy ONE Tenant Service and point all your multi-tenant projects to it.

---

## Security Best Practices

### **1. Never Hardcode Secrets in `appsettings.json`**

#### **❌ Bad: Hardcoded Secret**

```json
{
  "Jwt": {
    "Secret": "CHANGE_ME_JWT_SECRET"
  }
}
```

#### **✅ Good: Use Environment Variables**

**Development - `appsettings.Development.json`:**

```json
{
  "Jwt": {
    "Secret": "dev-secret-only-for-local-testing"
  }
}
```

**Production - Environment Variables:**

```bash
# Azure App Service
az webapp config appsettings set --name identity-api-prod --resource-group MyRG --settings JWT_SECRET_KEY="CHANGE_ME_JWT_SECRET"

# Docker
docker run -e JWT_SECRET_KEY="CHANGE_ME_JWT_SECRET" identity-service

# Kubernetes
kubectl create secret generic jwt-secret --from-literal=JWT_SECRET_KEY="CHANGE_ME_JWT_SECRET"
```

**Program.cs - Read from Environment:**

```csharp
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new InvalidOperationException("JWT secret key not configured");

options.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
```

### **2. Use HTTPS Only in Production**

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
```

### **3. Implement Token Rotation**

**Rotate JWT secret keys periodically (every 6-12 months):**

```csharp
// Support multiple keys during transition period
var primaryKey = builder.Configuration["Jwt:Secret"];
var secondaryKey = builder.Configuration["Jwt:SecondarySecret"]; // Old key

options.TokenValidationParameters = new TokenValidationParameters
{
    // ... other settings ...
    IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
    {
        var keys = new List<SecurityKey>
        {
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(primaryKey)),
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secondaryKey))
        };
        return keys;
    }
};
```

**Rotation Process:**

1. Deploy new key as `SecondaryKey` (keeps old key as primary)
2. Wait 24 hours (all tokens still valid)
3. Swap: New key becomes primary, old key becomes secondary
4. Wait 60 minutes (token expiry time)
5. Remove old key

### **4. Short Token Expiration**

```json
{
  "Jwt": {
    "AccessTokenExpirationMinutes": 15, // Short-lived access token
    "RefreshTokenExpirationDays": 7 // Longer refresh token
  }
}
```

**Benefits:**

- ✅ Reduces window for stolen token abuse
- ✅ Forces re-authentication via refresh token
- ✅ Allows role/permission changes to take effect quickly

### **5. Implement Refresh Token Rotation**

```csharp
[HttpPost("refresh")]
public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
{
    // 1. Validate refresh token
    var storedToken = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == request.RefreshToken);
    if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
        return Unauthorized("Invalid refresh token");

    // 2. Generate new access token + new refresh token
    var newAccessToken = GenerateAccessToken(storedToken.UserId);
    var newRefreshToken = GenerateRefreshToken();

    // 3. Revoke old refresh token (one-time use)
    storedToken.RevokedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();

    // 4. Store new refresh token
    await _db.RefreshTokens.AddAsync(new RefreshToken
    {
        Token = newRefreshToken,
        UserId = storedToken.UserId,
        ExpiresAt = DateTime.UtcNow.AddDays(7)
    });
    await _db.SaveChangesAsync();

    return Ok(new { accessToken = newAccessToken, refreshToken = newRefreshToken });
}
```

### **6. Rate Limiting on Authentication Endpoints**

```csharp
using AspNetCoreRateLimit;

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/login",
            Limit = 5,        // 5 attempts
            Period = "1m"     // per minute
        }
    };
});

app.UseIpRateLimiting();
```

### **7. Audit Logging**

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    try
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        // Log successful login
        _logger.LogInformation("User {Email} logged in successfully from IP {IP}",
            request.Email,
            HttpContext.Connection.RemoteIpAddress);

        return Ok(result);
    }
    catch (Exception ex)
    {
        // Log failed login attempt
        _logger.LogWarning("Failed login attempt for {Email} from IP {IP}: {Error}",
            request.Email,
            HttpContext.Connection.RemoteIpAddress,
            ex.Message);

        return Unauthorized("Invalid credentials");
    }
}
```

---

## Troubleshooting

### **Problem 1: "Unauthorized" when calling Project A/B**

#### **Symptoms:**

```json
{
  "status": 401,
  "title": "Unauthorized"
}
```

#### **Possible Causes & Solutions:**

**Cause 1: Missing or malformed `Authorization` header**

```bash
# ❌ Wrong
curl https://projecta.com/api/orders

# ✅ Correct
curl -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." https://projecta.com/api/orders
```

**Cause 2: JWT secret mismatch**

```json
// Identity Service uses different secret than Project A
// Identity appsettings.json
"Jwt": { "Secret": "secret-abc123..." }

// Project A appsettings.json (WRONG - different secret)
"Jwt": { "Secret": "secret-xyz789..." }

// ✅ FIX: Use SAME secret in all services
```

**Cause 3: Issuer/Audience mismatch**

```csharp
// Identity Service generates token with:
Issuer = "IhsanDev"
Audience = "MicroservicesApp"

// Project A validates with DIFFERENT values (WRONG)
ValidIssuer = "DifferentIssuer"
ValidAudience = "DifferentAudience"

// ✅ FIX: Use SAME issuer/audience in all services
```

**Cause 4: Expired token**

```bash
# Check token expiration
# Decode JWT at https://jwt.io
# Look for "exp" claim (Unix timestamp)

# ✅ FIX: Login again to get fresh token
```

### **Problem 2: "Forbidden" (403) when calling endpoint**

#### **Symptoms:**

```json
{
  "status": 403,
  "title": "Forbidden"
}
```

#### **Cause: User lacks required role**

```csharp
[Authorize(Roles = "Admin")] // Endpoint requires Admin role
public IActionResult CreateOrder() { }
```

**User's JWT has:**

```json
{ "role": "User" } // ❌ Not "Admin"
```

**✅ FIX:** Assign Admin role to user in Identity Service

### **Problem 3: Authentication middleware not working**

#### **Cause: Middleware order incorrect**

```csharp
// ❌ WRONG ORDER
app.UseAuthorization();  // Authorization before authentication
app.UseAuthentication();

// ✅ CORRECT ORDER
app.UseAuthentication();  // Must come first
app.UseAuthorization();
```

### **Problem 4: Cannot extract user claims**

```csharp
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;  // Returns null
```

#### **Cause: Claims not added to token**

Check Identity Service token generation:

```csharp
var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),  // ← Must be added
    new Claim(ClaimTypes.Email, user.Email),
    new Claim(ClaimTypes.Role, user.Role)
};

var token = new JwtSecurityToken(
    issuer: _configuration["Jwt:Issuer"],
    audience: _configuration["Jwt:Audience"],
    claims: claims,  // ← Must include claims
    expires: DateTime.UtcNow.AddMinutes(60),
    signingCredentials: credentials
);
```

---

## Summary & Action Plan

### **✅ Recommended Approach: Shared Identity Service**

| Aspect                | Details                                                 |
| --------------------- | ------------------------------------------------------- |
| **Architecture**      | 1 Identity Service → N Projects (A, B, C, ...)          |
| **User Experience**   | Login once, access all projects (SSO)                   |
| **JWT Configuration** | Same `Secret`, `Issuer`, `Audience` across all projects |
| **User Database**     | Single PostgreSQL database in Identity Service          |
| **Token Validation**  | Each project validates JWT using shared secret key      |
| **Maintenance**       | Update authentication logic once, applies everywhere    |
| **Cost**              | 1 database + 1 service = Minimal cost                   |

### **📋 Implementation Checklist**

#### **For Identity Service (One-Time Setup)**

- [ ] Deploy Identity Service to production (e.g., `https://identity-api.azure.com`)
- [ ] Configure PostgreSQL database for users
- [ ] Set up JWT secret key in environment variables (not hardcoded)
- [ ] Configure HTTPS and CORS
- [ ] Implement refresh token rotation
- [ ] Add rate limiting to login endpoint
- [ ] Set up audit logging
- [ ] Test login/register endpoints

#### **For Tenant Service (One-Time Setup - IF using multi-tenancy)**

- [ ] Deploy Tenant Service to production (e.g., `https://tenant-api.azure.com`)
- [ ] Configure PostgreSQL database for tenants
- [ ] Secure with JWT authentication (same as Identity Service)
- [ ] Set up CORS for allowed origins
- [ ] Test endpoints: GET /api/tenants/{id}
- [ ] Verify caching is working (check logs for cache hits/misses)

#### **For Each Project (A, B, C, ...) - Authentication**

- [ ] Add `Microsoft.AspNetCore.Authentication.JwtBearer` package
- [ ] Copy `Jwt` configuration to `appsettings.json` (same key/issuer/audience)
- [ ] Add `AddAuthentication()` with JWT configuration in `Program.cs`
- [ ] Add `UseAuthentication()` and `UseAuthorization()` middleware
- [ ] Add `[Authorize]` attribute to protected endpoints
- [ ] Extract user claims using `User.FindFirst(ClaimTypes.NameIdentifier)`
- [ ] Test authentication with Postman/curl

#### **For Each Project That Needs Multi-Tenancy (Optional)**

- [ ] Set `MultiTenancy:Enabled = true` in `appsettings.json`
- [ ] Set `MultiTenancy:TenantServiceUrl = "https://tenant-api.azure.com"`
- [ ] Add `builder.Services.AddMultiTenancy(configuration)` in `Program.cs`
- [ ] Add `app.UseTenantResolution()` before authentication middleware
- [ ] Inject `ITenantContext` in services that need tenant filtering
- [ ] Test with `x-tenant-id` header in requests
- [ ] Verify caching is reducing API calls (check performance metrics)

### **🔑 Key Configuration Values (Must Be Identical)**

**For Authentication (All Projects):**

```json
{
  "Jwt": {
    "Secret": "<SAME_SECRET_KEY_FOR_ALL_SERVICES>",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp"
  }
}
```

**For Multi-Tenancy (Only Multi-Tenant Projects):**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://tenant-api-prod.azurewebsites.net", // ← SAME for all projects
    "CacheExpirationMinutes": 5
  }
}
```

### **🚀 Quick Start Example**

**1. Get JWT Token from Identity Service:**

```bash
curl -X POST https://identity-api.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"john@example.com","password":"SecurePass123!"}'

# Response:
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "expiresIn": 3600
}
```

**2. Use Token in Project A:**

```bash
curl -X GET https://projecta.com/api/orders \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

# Response:
{
  "orders": [...]
}
```

**3. Use SAME Token in Project B:**

```bash
curl -X GET https://projectb.com/api/inventory \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

# Response:
{
  "inventory": [...]
}
```

**4. (OPTIONAL) Add Tenant Context for Multi-Tenant Projects:**

```bash
# Project A with tenant filtering
curl -X GET https://projecta.com/api/orders \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "x-tenant-id: 123"

# Response: Orders filtered for tenant 123
{
  "orders": [
    { "id": 1, "tenantId": 123, "product": "Widget" }
  ]
}

# Project B with same tenant
curl -X GET https://projectb.com/api/inventory \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "x-tenant-id: 123"

# Response: Inventory filtered for tenant 123
{
  "inventory": [
    { "id": 1, "tenantId": 123, "item": "Widget", "quantity": 50 }
  ]
}
```

### **📚 Related Documentation**

- **NEW_SERVICE_INTEGRATION_GUIDE.md** - Detailed authentication setup for new services
- **TENANT_MIDDLEWARE_EXPLAINED.md** - Multi-tenancy integration (optional)
- **CACHING_STRATEGY_COMPARISON.md** - Caching decisions for configuration data
- **FILE_MANAGER_SERVICE_GUIDE.md** - File storage service architecture (NEW)
- **Identity.API Documentation** - Your Identity Service API reference

---

## Conclusion

**Use ONE shared Identity Service AND ONE shared Tenant Service across all your projects.** This is:

- ✅ **Industry standard** for microservices
- ✅ **Better user experience** (SSO, consistent tenant settings)
- ✅ **More cost effective** (2 shared services vs N×2 services)
- ✅ **Easier to maintain** (update once, apply everywhere)
- ✅ **More secure** (centralized security updates)

**Your current architecture is already configured to support this pattern** - no changes needed! Just:

1. **Deploy Identity Service once** → All projects use for authentication
2. **Deploy Tenant Service once** → Multi-tenant projects use for tenant data
3. **Configure each project** → Point to same shared services

### **Deployment Summary**

```
┌─────────────────────────────────────────────────────────────┐
│               SHARED SERVICES (Deploy ONCE)                  │
│                                                               │
│  ┌─────────────────────┐    ┌──────────────────────┐        │
│  │  Identity Service   │    │  Tenant Service      │        │
│  │  (Port 5001)        │    │  (Port 5002)         │        │
│  │  • User Auth        │    │  • Tenant Config     │        │
│  │  • JWT Generation   │    │  • Multi-tenancy     │        │
│  └─────────────────────┘    └──────────────────────┘        │
│           ▲                           ▲                      │
└───────────┼───────────────────────────┼──────────────────────┘
            │                           │
            │                           │ (only if multi-tenant)
    ┌───────┴───────────────────────────┴──────┐
    │                                           │
    ▼                                           ▼
┌─────────────────┐                    ┌─────────────────┐
│   PROJECT A     │                    │   PROJECT B     │
│   (Port 5003)   │                    │   (Port 5004)   │
│                 │                    │                 │
│  • Validates    │                    │  • Validates    │
│    JWT tokens   │                    │    JWT tokens   │
│  • Calls Tenant │                    │  • Calls Tenant │
│    Service      │                    │    Service      │
│  • Caches       │                    │  • Caches       │
│    tenant data  │                    │    tenant data  │
└─────────────────┘                    └─────────────────┘
```

**Total Infrastructure:**

- 2-3 shared services (Identity + Tenant + optional File Manager)
- N project services (each points to shared services)
- Lower cost, better consistency, easier management

**Note:** File Manager Service is optional - add when you need centralized file storage. See **FILE_MANAGER_SERVICE_GUIDE.md** for details.

---

**Last Updated:** October 19, 2025  
**Version:** 2.1.0 (Added File Manager Service reference)
