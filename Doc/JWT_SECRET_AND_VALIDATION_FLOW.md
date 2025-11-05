# How JWT Secret Works in Notification Service

## Overview

The **Hub does NOT directly access the JWT secret**. Instead, ASP.NET Core's **JWT Bearer Authentication Middleware** validates the token BEFORE it reaches the Hub. The Hub only reads the validated claims from `Context.User`.

---

## Complete JWT Validation Flow

### **Step 1: Configuration at Application Startup**

#### **Program.cs - JWT Secret Configuration**

```csharp
// Read JWT settings from appsettings.json
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JWT Secret is not configured");

// Configure JWT Authentication Middleware
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // THIS IS WHERE THE SECRET IS USED
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        
        // Convert secret string to cryptographic key
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey)
        ),
        
        ValidateIssuer = true,
        ValidIssuer = "IdentityService",
        
        ValidateAudience = true,
        ValidAudience = "MicroservicesApp",
        
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});
```

#### **appsettings.json - Secret Storage**

```json
{
  "Jwt": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters-must-match-identity-service",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 60
  }
}
```

**Important**: This secret MUST be the **same** across all services (Identity, Notification, Tenant, etc.)

---

### **Step 2: Client Sends Request with JWT**

#### **SignalR Connection**

```javascript
// Client includes JWT token
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications", {
        accessTokenFactory: () => "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    })
    .build();

await connection.start();
```

**OR** via query string (for WebSocket upgrade):

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications?access_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...")
    .build();
```

#### **HTTP Request Format**

```http
GET /hubs/notifications HTTP/1.1
Host: localhost:5002
Upgrade: websocket
Connection: Upgrade

# Option 1: Authorization Header
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

# Option 2: Query String (for WebSocket)
/hubs/notifications?access_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

### **Step 3: JWT Middleware Extracts Token**

#### **OnMessageReceived Event**

The middleware is configured to extract tokens from query strings for SignalR:

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        // Extract token from query string
        var accessToken = context.Request.Query["access_token"];

        // Check if request is for SignalR hub
        var path = context.HttpContext.Request.Path;
        if (!string.IsNullOrEmpty(accessToken) && 
            path.StartsWithSegments("/hubs/notifications"))
        {
            // Set token for validation
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
};
```

**Why?** WebSocket connections can't send Authorization headers after upgrade, so token must be in URL.

---

### **Step 4: JWT Middleware Validates Token**

#### **Validation Process**

The middleware automatically validates the token using the configured secret:

```csharp
// This happens AUTOMATICALLY in the middleware
// You don't write this code - it's built into ASP.NET Core

// 1. Decode JWT (Base64 decode)
var header = DecodeBase64(token.Split('.')[0]);
var payload = DecodeBase64(token.Split('.')[1]);
var signature = token.Split('.')[2];

// 2. Verify signature using secret key
var computedSignature = HMACSHA256(
    header + "." + payload, 
    secretKey  // From appsettings.json
);

// 3. Compare signatures
if (computedSignature != signature)
{
    // INVALID TOKEN - Return 401 Unauthorized
    context.Response.StatusCode = 401;
    return;
}

// 4. Validate claims
if (payload.exp < DateTime.UtcNow.ToUnixTime())
{
    // EXPIRED TOKEN - Return 401 Unauthorized
    return;
}

if (payload.iss != "IdentityService")
{
    // INVALID ISSUER - Return 401 Unauthorized
    return;
}

if (payload.aud != "MicroservicesApp")
{
    // INVALID AUDIENCE - Return 401 Unauthorized
    return;
}

// 5. Token is VALID - Create ClaimsPrincipal
context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
```

#### **What Gets Validated?**

| Check | Description | Config Source |
|-------|-------------|---------------|
| **Signature** | HMAC SHA256 hash matches | `Jwt:Secret` |
| **Issuer** | `iss` claim = "IdentityService" | `Jwt:Issuer` |
| **Audience** | `aud` claim = "MicroservicesApp" | `Jwt:Audience` |
| **Expiration** | `exp` claim > current time | Built-in |
| **Not Before** | `nbf` claim < current time | Built-in |

---

### **Step 5: Middleware Populates HttpContext.User**

If validation succeeds, the middleware creates a `ClaimsPrincipal` with all claims from the JWT:

```csharp
// HttpContext.User now contains:
context.User.Claims = [
    { Type: "sub", Value: "1" },                    // User ID
    { Type: "unique_name", Value: "john.doe" },
    { Type: "email", Value: "john@ihsandev.com" },
    { Type: "tenantId", Value: "ihsandev" },
    { Type: "iss", Value: "IdentityService" },
    { Type: "aud", Value: "MicroservicesApp" },
    { Type: "exp", Value: "1730812800" },
    { Type: "iat", Value: "1730809200" }
];

context.User.Identity.IsAuthenticated = true;
```

---

### **Step 6: Hub Accesses Validated Claims**

#### **NotificationHub.OnConnectedAsync()**

The Hub can now safely read claims without worrying about validation:

```csharp
public override async Task OnConnectedAsync()
{
    // JWT has ALREADY been validated by middleware
    // Hub just reads the validated claims from Context.User
    
    var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
    var userId = userIdClaim?.Value;  // "1"
    
    var isAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false;
    
    if (isAuthenticated)
    {
        // User has valid JWT token
        // Claims have been validated using the secret
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
    }
    else
    {
        // No valid JWT token
        // Only add to global group
        await Groups.AddToGroupAsync(Context.ConnectionId, "global");
    }
}
```

**Key Point**: The Hub **never sees the secret key**. It only reads pre-validated claims.

---

## Middleware Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│                    ASP.NET CORE PIPELINE                         │
└─────────────────────────────────────────────────────────────────┘

1. Client Request
   ├─ GET /hubs/notifications
   ├─ Authorization: Bearer eyJhbGci...
   └─ x-tenant-id: ihsandev

2. CORS Middleware
   └─ Check allowed origins

3. Authentication Middleware ★ JWT VALIDATION HAPPENS HERE ★
   ├─ Extract token (from header or query string)
   ├─ Decode JWT
   ├─ Verify signature using SECRET KEY
   ├─ Validate issuer, audience, expiration
   ├─ If VALID: Populate HttpContext.User with claims
   └─ If INVALID: Return 401 Unauthorized

4. Authorization Middleware
   └─ Check if user has required permissions

5. SignalR Hub
   ├─ OnConnectedAsync() called
   ├─ Read Context.User.Claims (already validated)
   └─ Join appropriate groups

6. Response
   └─ WebSocket connection established (if authenticated)
```

---

## Per-Tenant JWT Support

For multi-tenant applications, each tenant can have their own JWT secret:

```csharp
// Program.cs
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        // ... extract token ...
        
        // Support per-tenant JWT validation
        if (jwtMode == JwtMode.PerTenant)
        {
            var tenantContext = context.HttpContext.RequestServices
                .GetService<ITenantContext>();
            
            if (tenantContext?.HasTenant == true && 
                tenantContext.CurrentTenant?.Configuration?.Jwt != null)
            {
                var tenantJwt = tenantContext.CurrentTenant.Configuration.Jwt;
                
                // Override with tenant-specific secret
                if (!string.IsNullOrEmpty(tenantJwt.Secret))
                {
                    context.Options.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(tenantJwt.Secret)
                        );
                    context.Options.TokenValidationParameters.ValidIssuer = 
                        tenantJwt.Issuer;
                    context.Options.TokenValidationParameters.ValidAudience = 
                        tenantJwt.Audience;
                }
            }
        }
        
        return Task.CompletedTask;
    }
};
```

### **How Tenant-Specific Secrets Work**

1. **Tenant Service** stores JWT configuration per tenant:
```json
{
  "tenantId": "ihsandev",
  "configuration": {
    "jwt": {
      "secret": "ihsandev-specific-secret-key-32-chars",
      "issuer": "IhsanDevIdentity",
      "audience": "IhsanDevApp"
    }
  }
}
```

2. **Notification Service** fetches tenant config based on `x-tenant-id` header

3. **Middleware** uses tenant-specific secret to validate JWT

---

## Security Best Practices

### **1. Secret Key Storage**

❌ **Bad** - Hardcoded in code:
```csharp
var secret = "my-secret-key";  // DON'T DO THIS
```

✅ **Good** - Configuration file:
```json
// appsettings.json (for development)
{
  "Jwt": {
    "Secret": "dev-secret-key-32-characters-long"
  }
}
```

✅ **Better** - Environment variables:
```bash
# Production
export JWT__SECRET="prod-secret-key-from-azure-keyvault"
```

✅ **Best** - Azure Key Vault / AWS Secrets Manager:
```csharp
builder.Configuration.AddAzureKeyVault(
    vaultUri: "https://myvault.vault.azure.net/",
    credential: new DefaultAzureCredential()
);

var secret = builder.Configuration["Jwt:Secret"];  // From Key Vault
```

### **2. Secret Rotation**

When rotating secrets:
1. Add new secret alongside old secret
2. Update Identity Service to sign with new secret
3. Keep validating with both secrets for transition period
4. Remove old secret after all tokens expire

### **3. Secret Length**

- ❌ Too short: < 32 characters (weak)
- ✅ Minimum: 32 characters
- ✅ Recommended: 64+ characters

```csharp
// Generate strong secret
var secret = Convert.ToBase64String(
    System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)
);
// Example: "QYtKXQ7J9xR2mL5nP8vW3zC6fH1bN4dG7kS0uA..."
```

---

## Secret Synchronization

### **Cross-Service Secret Sharing**

All services that validate JWTs must use the **same secret**:

```
┌─────────────────┐
│ Identity Service│ ← Signs JWTs with secret
└────────┬────────┘
         │
    Same Secret
         │
    ┌────┴────┬─────────┬──────────┐
    │         │         │          │
┌───▼──┐  ┌──▼──┐  ┌───▼───┐  ┌──▼──┐
│Notify│  │Tenant│  │ Other │  │  API│
│ Svc  │  │ Svc  │  │  Svc  │  │  Svc│
└──────┘  └──────┘  └───────┘  └─────┘
    ↑         ↑          ↑         ↑
    └─────────┴──────────┴─────────┘
         All validate with same secret
```

### **Configuration Management**

**Option 1: Shared Configuration**
```bash
# All services read from same config source
export JWT__SECRET="shared-secret-key"
```

**Option 2: Configuration Server**
```csharp
// Spring Cloud Config / Azure App Configuration
builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect("connection-string")
           .Select("Jwt:*");
});
```

**Option 3: Per-Tenant (Advanced)**
- Each tenant has own secret
- Stored in Tenant Service database
- Fetched dynamically during validation

---

## Troubleshooting

### **Problem: 401 Unauthorized on Hub Connection**

**Possible Causes:**

1. **Secret Mismatch**
   ```
   Identity Service: Signs with "secret-A"
   Notification Service: Validates with "secret-B"
   Result: Signature verification fails → 401
   ```

2. **Token Expired**
   ```
   JWT exp claim: 1730809200 (Nov 5, 2025 14:00)
   Current time:  1730812800 (Nov 5, 2025 15:00)
   Result: Expired token → 401
   ```

3. **Wrong Issuer/Audience**
   ```
   JWT iss: "WrongIssuer"
   Expected: "IdentityService"
   Result: Validation fails → 401
   ```

4. **Token Not Sent**
   ```
   No Authorization header
   No access_token query param
   Result: Anonymous connection (if hub allows)
   ```

### **Debugging Steps**

1. **Enable Detailed Errors**
   ```json
   {
     "SignalR": {
       "EnableDetailedErrors": true
     }
   }
   ```

2. **Check Logs**
   ```bash
   # Look for JWT validation errors
   dotnet run
   # Check output for:
   # - "Token signature validation failed"
   # - "Token expired"
   # - "Invalid issuer"
   ```

3. **Decode JWT** (without secret)
   ```bash
   # Visit https://jwt.io
   # Paste token to see claims (header + payload only)
   # Verify exp, iss, aud claims
   ```

4. **Verify Secret**
   ```csharp
   // Add logging in Program.cs
   var secret = builder.Configuration["Jwt:Secret"];
   Console.WriteLine($"JWT Secret Length: {secret?.Length ?? 0}");
   Console.WriteLine($"JWT Issuer: {builder.Configuration["Jwt:Issuer"]}");
   ```

---

## Summary

### **Key Points:**

1. ✅ **Hub NEVER accesses the JWT secret directly**
2. ✅ **Middleware validates tokens BEFORE hub connection**
3. ✅ **Hub only reads pre-validated claims from `Context.User`**
4. ✅ **Secret must be shared across all services**
5. ✅ **Secret stored in configuration (appsettings.json or Key Vault)**
6. ✅ **Middleware uses secret to verify JWT signature**
7. ✅ **Per-tenant secrets supported for multi-tenancy**

### **Validation Flow:**

```
Client JWT → Middleware (uses secret) → Validates → Populates Context.User → Hub reads claims
              ▲
              │
         Secret from
      appsettings.json
```

The Hub is **completely decoupled** from JWT validation logic - it just trusts that `Context.User` contains valid, authenticated claims!
