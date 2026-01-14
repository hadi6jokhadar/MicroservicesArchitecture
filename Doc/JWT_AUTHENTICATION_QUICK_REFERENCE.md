# JWT Authentication - Quick Reference

## Usage Examples

### Basic Usage (Most Services)

For services that support multi-tenancy with per-tenant JWT:

```csharp
// In Program.cs
builder.Services.AddJwtAuthentication(builder.Configuration);
```

That's it! This single line provides:

- ✅ JWT authentication from appsettings.json
- ✅ Per-tenant JWT support (when configured)
- ✅ Automatic fallback to global JWT
- ✅ Standard logging events

---

### Shared JWT Only (No Multi-Tenancy)

For services that only use global JWT:

```csharp
// In Program.cs
builder.Services.AddJwtAuthenticationSharedOnly(builder.Configuration);
```

Use when:

- Service doesn't support multi-tenancy
- All users authenticate with the same JWT configuration
- Example: Tenant management service

---

### Custom Token Extraction (SignalR, WebSockets)

For services with special authentication needs:

```csharp
// In Program.cs
builder.Services.AddJwtAuthentication(
    builder.Configuration,
    enablePerTenantJwt: true,
    customMessageReceived: context =>
    {
        // Extract token from query string (for WebSocket connections)
        if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
        {
            var token = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
        }
        return Task.CompletedTask;
    });
```

Use when:

- Need to extract JWT from non-standard locations (query string, cookies, etc.)
- SignalR hubs (tokens come from query string)
- Custom authentication flows

---

## Configuration

### Required Settings (appsettings.json)

```json
{
  "Jwt": {
    "Secret": "your-256-bit-secret-key-here",
    "Issuer": "YourAppName",
    "Audience": "YourAppName"
  }
}
```

### Optional Multi-Tenancy Settings

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant" // "Shared" or "PerTenant"
  }
}
```

**JWT Modes:**

- `Shared` - All tenants use the same JWT configuration from appsettings.json
- `PerTenant` - Each tenant can have their own JWT configuration in the database

---

## HTTP Headers

### Standard Authentication

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### With Tenant Context (Multi-Tenant)

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
x-tenant-id: tenant-123
```

### SignalR Connection (Query String)

```
/hubs/notifications?access_token=eyJhbGciOiJIUzI1NiIs...&tenantId=tenant-123
```

---

## Common Scenarios

### 1. Add JWT to New Service

```csharp
// Program.cs
builder.Services.AddJwtAuthentication(builder.Configuration);

// ... later in middleware pipeline
app.UseAuthentication();
app.UseAuthorization();
```

### 2. Protect an Endpoint

```csharp
app.MapGet("/api/secure", [Authorize] () =>
{
    return "This endpoint requires authentication";
});
```

### 3. Get Current User ID

```csharp
app.MapGet("/api/me", [Authorize] (ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return new { userId };
});
```

### 4. Role-Based Authorization

```csharp
app.MapGet("/api/admin", [Authorize(Roles = "Admin")] () =>
{
    return "Admin only";
});
```

---

## Logging Output

The shared extension provides standardized logging:

### Successful Authentication

```
🔐 Using tenant-specific JWT validation for tenant: tenant-123 (Issuer: TenantApp)
JWT Token Validated - User ID: user-456, Path: /api/files
```

### Fallback to Global JWT

```
Tenant tenant-123 has no JWT configuration, falling back to global JWT
Using global JWT validation - Secret: 64 chars, Issuer: MyApp
JWT Token Validated - User ID: user-456, Path: /api/files
```

### Authentication Failure

```
JWT Authentication Failed - Path: /api/secure, Error: IDX10223: Lifetime validation failed...
```

---

## Troubleshooting

### Issue: 401 Unauthorized

**Check:**

1. JWT Secret matches between token generation and validation
2. Token hasn't expired (check `exp` claim)
3. Issuer and Audience match configuration
4. Token is properly formatted: `Bearer <token>`

**Debug:**

```csharp
// Add detailed error logging in appsettings.json
"Logging": {
  "LogLevel": {
    "Microsoft.AspNetCore.Authentication": "Debug"
  }
}
```

### Issue: Per-Tenant JWT Not Working

**Check:**

1. `MultiTenancy:JwtMode` is set to `PerTenant`
2. `x-tenant-id` header is present
3. Tenant has JWT configuration in database
4. Tenant's JWT secret is not empty

**Debug:**
Look for these log messages:

- "Using tenant-specific JWT validation for tenant: {TenantId}"
- "Tenant {TenantId} has no JWT configuration"

### Issue: SignalR Authentication Fails

**Check:**

1. Token passed in query string: `access_token`
2. Custom message handler configured correctly
3. CORS allows credentials
4. Token extraction happens before validation

---

## Best Practices

### ✅ DO

- Use environment variables for JWT secrets in production
- Rotate JWT secrets periodically
- Keep tokens short-lived (15-60 minutes)
- Use HTTPS in production
- Log authentication failures for security monitoring

### ❌ DON'T

- Commit JWT secrets to source control
- Use weak or predictable secrets
- Store tokens in localStorage (XSS risk)
- Share secrets between environments
- Use the same secret for all tenants in production

---

## API Reference

### AddJwtAuthentication

```csharp
public static IServiceCollection AddJwtAuthentication(
    this IServiceCollection services,
    IConfiguration configuration,
    bool enablePerTenantJwt = true,
    Func<MessageReceivedContext, Task>? customMessageReceived = null)
```

**Parameters:**

- `configuration` - App configuration containing JWT settings
- `enablePerTenantJwt` - Enable per-tenant JWT validation (default: true)
- `customMessageReceived` - Optional custom token extraction handler

**Returns:** `IServiceCollection` for method chaining

---

### AddJwtAuthenticationSharedOnly

```csharp
public static IServiceCollection AddJwtAuthenticationSharedOnly(
    this IServiceCollection services,
    IConfiguration configuration)
```

**Parameters:**

- `configuration` - App configuration containing JWT settings

**Returns:** `IServiceCollection` for method chaining

---

## JWT Token Contents

### Standard Claims

All JWT tokens include these standard claims:

```json
{
  "sub": "123", // User ID (NameIdentifier)
  "email": "user@example.com", // User email
  "given_name": "John", // First name
  "family_name": "Doe", // Last name
  "role": ["User", "Admin"], // User roles (array)
  "permission:read": "true", // Custom claims (permissions)
  "permission:write": "true",
  "tenant_id": "tenant-001", // Tenant ID (if multi-tenant)
  "exp": 1737123456, // Expiration timestamp
  "iss": "IdentityService", // Issuer
  "aud": "MicroservicesApp" // Audience
}
```

### Role Claims

**Automatically Included (January 2026):**

- All user roles are added as `ClaimTypes.Role` claims
- All custom permissions are added as custom claims
- Roles and claims are loaded with navigation properties
- No manual intervention required

**Authorization Usage:**

```csharp
// Check single role
[Authorize(Roles = "Admin")]

// Check multiple roles
[Authorize(Roles = "Admin,SuperAdmin")]

// Check custom claim
[Authorize(Policy = "RequireReadPermission")]

// Get current user's roles in code
var roles = _currentUserService.Roles; // IEnumerable<string>
bool isAdmin = _currentUserService.HasRole("Admin");
bool isSuperAdmin = _currentUserService.IsSuperAdmin;
```

### Response Body Behavior

**Important:** Roles and claims are **always in JWT tokens**, but their visibility in response bodies is conditional:

| Endpoint                 | Roles in Response            | Reason                                         |
| ------------------------ | ---------------------------- | ---------------------------------------------- |
| POST /auth/login         | ❌ No                        | Reduces response size, roles are in JWT        |
| POST /auth/register      | ❌ No                        | Reduces response size, roles are in JWT        |
| POST /auth/refresh-token | ❌ No                        | Reduces response size, roles are in JWT        |
| GET /users/profile       | ✅ Only for Admin/SuperAdmin | Privacy - regular users don't see role configs |
| PUT /users/profile       | ✅ Only for Admin/SuperAdmin | Privacy - regular users don't see role configs |
| GET /admin/users/:id     | ✅ Always                    | Admin endpoints show full details              |
| GET /admin/users         | ✅ Always                    | Admin endpoints show full details              |
| POST /admin/users        | ✅ Always                    | Admin endpoints show full details              |

**Why this design?**

- **Security**: Non-admin users can't inspect role/claim configurations
- **Performance**: Smaller response payloads for authentication
- **Consistency**: JWT is single source of truth for authorization
- **Privacy**: Role details only visible to administrators

---

## Examples by Service

### FileManager

```csharp
builder.Services.AddJwtAuthentication(builder.Configuration);
```

### Identity

```csharp
builder.Services.AddJwtAuthentication(builder.Configuration);
```

### Tenant

```csharp
builder.Services.AddJwtAuthenticationSharedOnly(builder.Configuration);
```

### Notification (with SignalR)

```csharp
builder.Services.AddJwtAuthentication(
    builder.Configuration,
    customMessageReceived: context =>
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
        {
            var token = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
        }
        return Task.CompletedTask;
    });
```

---

## Related Documentation

- [JWT_AUTHENTICATION_CONSOLIDATION.md](JWT_AUTHENTICATION_CONSOLIDATION.md) - Full implementation details
- [JWT_TENANT_VERIFICATION_GUIDE.md](JWT_TENANT_VERIFICATION_GUIDE.md) - Multi-tenant setup
- [JWT_SECRET_AND_VALIDATION_FLOW.md](JWT_SECRET_AND_VALIDATION_FLOW.md) - Security guide
- [IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md](IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md) - Role claims implementation

---

_Last Updated: January 13, 2026_
