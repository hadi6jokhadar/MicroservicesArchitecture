# 🔐 JWT Tenant-Specific Settings Verification Guide

## Overview

This guide shows you how to verify that JWT tokens are being generated using **tenant-specific JWT settings** from the database (when configured) rather than the default settings from `appsettings.json`.

---

## How It Works Now

The `UserService` has been updated to check for tenant-specific JWT settings and log detailed information about which settings are being used.

### Token Generation Priority

```
1. Check if multi-tenancy is enabled (MultiTenancy:Enabled = true)
   ↓
2. Check if tenant context exists (x-tenant-id header was provided)
   ↓
3. Check if tenant has custom JWT configuration
   ↓
4. Use tenant JWT settings if available, otherwise use appsettings.json
   ↓
5. Log which settings were used (with 🔐 emoji for easy identification)
```

---

## Verification Methods

### Method 1: Check Application Logs (Easiest)

When a token is generated, you'll see detailed log messages like these:

#### **Scenario A: Using Tenant-Specific JWT Settings**

```
[Information] 🔐 Generating JWT token using TENANT-SPECIFIC settings for tenant 'acme-corp-123' (Issuer: AcmeCorp, Expiry: 120 min)
[Debug] Added tenant_id claim: acme-corp-123
[Information] ✅ JWT token generated successfully for user 'john@acme.com' (UserId: 1, Expires: 10/27/2025 2:45:00 PM)
```

#### **Scenario B: Tenant Has No Custom JWT (Uses Default)**

```
[Information] 🔐 Generating JWT token using DEFAULT settings for tenant 'widget-inc-456' (tenant has no custom JWT config) (Issuer: IdentityService, Expiry: 60 min)
[Debug] Added tenant_id claim: widget-inc-456
[Information] ✅ JWT token generated successfully for user 'jane@widget.com' (UserId: 2, Expires: 10/27/2025 1:45:00 PM)
```

#### **Scenario C: No Tenant Context (Single-Tenant Mode)**

```
[Information] 🔐 Generating JWT token using DEFAULT settings (no tenant context) (Issuer: IdentityService, Expiry: 60 min)
[Information] ✅ JWT token generated successfully for user 'admin@example.com' (UserId: 3, Expires: 10/27/2025 1:45:00 PM)
```

**How to View Logs:**

1. **In Console (when running with `dotnet run`):**

   ```bash
   cd src/Services/Identity/Identity.API
   dotnet run
   ```

   Watch the console output when users log in or register.

2. **In Log Files:**

   ```
   C:\Users\YOUR_USERNAME\Desktop\Projects\MicroservicesArchitecture\Logs\Identity\
   ```

   Open the most recent log file and search for "🔐" or "Generating JWT token"

3. **Filter Logs by Emoji:**
   ```bash
   # PowerShell
   Select-String -Path "C:\...\Logs\Identity\*.log" -Pattern "🔐"
   ```

---

### Method 2: Decode JWT Token and Inspect Claims

#### **Step 1: Get the Token**

Make a login request:

```bash
POST https://localhost:5001/api/auth/login
x-tenant-id: acme-corp-123

{
  "email": "john@acme.com",
  "password": "SecurePass123!"
}
```

**Response:**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "userId": 1,
  "email": "john@acme.com"
}
```

#### **Step 2: Decode the Token**

Use [jwt.io](https://jwt.io) or a command-line tool:

**Online (jwt.io):**

1. Go to https://jwt.io
2. Paste your `accessToken` in the "Encoded" section
3. Check the "Decoded" section

**Command Line:**

```bash
# PowerShell
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
$payload = $token.Split('.')[1]
$padding = '=' * ((4 - ($payload.Length % 4)) % 4)
[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload + $padding)) | ConvertFrom-Json
```

#### **Step 3: Verify Token Properties**

**What to Check:**

```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "nameid": "1",
    "email": "john@acme.com",
    "given_name": "John",
    "family_name": "Doe",
    "role": "User",
    "tenant_id": "acme-corp-123", // ← Should match your tenant
    "exp": 1729959900,
    "iss": "AcmeCorp", // ← Should match tenant's Issuer (if tenant-specific)
    "aud": "MicroservicesApp" // ← Should match tenant's Audience
  }
}
```

**Verification Checklist:**

- ✅ `tenant_id` claim exists (if multi-tenancy is enabled)
- ✅ `iss` (Issuer) matches tenant-specific Issuer or default
- ✅ `aud` (Audience) matches tenant-specific Audience or default
- ✅ `exp` (Expiry) is correct based on tenant's `AccessTokenExpirationMinutes`

---

### Method 3: Compare Token Signatures

Tokens signed with different secrets will have different signatures.

#### **Test Setup:**

**Tenant Configuration (in Tenant Service database):**

```json
// Tenant 1: Custom JWT Secret
{
  "tenantId": "tenant-A",
  "configuration": {
    "jwt": {
      "secret": "tenant-A-custom-secret-key-32-chars-min",
      "issuer": "TenantA",
      "audience": "TenantAApp",
      "accessTokenExpirationMinutes": 120
    }
  }
}

// Tenant 2: Uses Default (no custom JWT)
{
  "tenantId": "tenant-B",
  "configuration": {
    "database": {
      "connectionString": "..."
    }
    // No JWT config - will use default from appsettings.json
  }
}
```

#### **Generate Tokens:**

**Token for Tenant A (with custom JWT):**

```bash
POST https://localhost:5001/api/auth/login
x-tenant-id: tenant-A

{
  "email": "user@tenantA.com",
  "password": "Password123!"
}
```

**Token for Tenant B (default JWT):**

```bash
POST https://localhost:5001/api/auth/login
x-tenant-id: tenant-B

{
  "email": "user@tenantB.com",
  "password": "Password123!"
}
```

#### **Compare:**

**Tenant A Token (custom secret):**

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.
eyJuYW1laWQiOiIxIiwidGVuYW50X2lkIjoidGVuYW50LUEiLCJpc3MiOiJUZW5hbnRBIn0.
SIGNATURE_USING_TENANT_A_SECRET  ← Different signature
```

**Tenant B Token (default secret):**

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.
eyJuYW1laWQiOiIyIiwidGVuYW50X2lkIjoidGVuYW50LUIiLCJpc3MiOiJJZGVudGl0eVNlcnZpY2UifQ.
SIGNATURE_USING_DEFAULT_SECRET  ← Different signature
```

**Key Observation:**

- Even with similar payloads, the **signature will be different** if different secrets are used
- Tenant A's token uses `iss: "TenantA"` (custom)
- Tenant B's token uses `iss: "IdentityService"` (default)

---

### Method 4: Token Validation Test

Verify that a token issued for one tenant cannot be validated by another tenant (in PerTenant JWT mode).

#### **Setup: PerTenant JWT Mode**

**appsettings.json (Identity Service):**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "JwtMode": "PerTenant" // Each tenant has unique secret
  }
}
```

#### **Test:**

**Step 1: Login as Tenant A User**

```bash
POST https://localhost:5001/api/auth/login
x-tenant-id: tenant-A

{
  "email": "user@tenantA.com",
  "password": "Password123!"
}
```

**Response:** Token A

**Step 2: Try to Use Token A for Tenant B**

```bash
GET https://localhost:5001/api/user/profile
x-tenant-id: tenant-B  // Different tenant!
Authorization: Bearer {TOKEN_A}
```

**Expected Result:**

```
401 Unauthorized
{
  "error": "Invalid token signature"
}
```

**Logs Should Show:**

```
[Warning] Token validation failed: Signature validation failed for tenant 'tenant-B'
```

**Why?**

- Token A was signed with Tenant A's secret
- Token validation for Tenant B uses Tenant B's secret
- Signatures don't match → Token rejected ✅

---

## Practical Testing Scenarios

### Scenario 1: Fresh Tenant with Custom JWT

**Step 1: Create Tenant with Custom JWT**

```bash
POST https://localhost:5002/api/admin/tenant
Authorization: Bearer {admin_token}

{
  "tenantId": "test-jwt-123",
  "tenantName": "Test Tenant",
  "userId": 1,
  "data": "{\"jwt\":{\"secret\":\"my-custom-secret-key-for-testing-32chars\",\"issuer\":\"TestTenant\",\"audience\":\"TestApp\",\"accessTokenExpirationMinutes\":90}}"
}
```

**Step 2: Register User for This Tenant**

```bash
POST https://localhost:5001/api/auth/register
x-tenant-id: test-jwt-123

{
  "email": "test@example.com",
  "password": "Test123!",
  "firstName": "Test",
  "lastName": "User"
}
```

**Step 3: Check Logs**

You should see:

```
[Information] 🔐 Generating JWT token using TENANT-SPECIFIC settings for tenant 'test-jwt-123' (Issuer: TestTenant, Expiry: 90 min)
[Information] ✅ JWT token generated successfully for user 'test@example.com' (UserId: 1, Expires: ...)
```

**Step 4: Decode Token**

Payload should contain:

```json
{
  "tenant_id": "test-jwt-123",
  "iss": "TestTenant",  // ← Custom issuer
  "aud": "TestApp",     // ← Custom audience
  "exp": ...            // ← 90 minutes from now (custom expiry)
}
```

---

### Scenario 2: Tenant Without Custom JWT

**Step 1: Create Tenant Without JWT Config**

```bash
POST https://localhost:5002/api/admin/tenant
Authorization: Bearer {admin_token}

{
  "tenantId": "no-jwt-tenant",
  "tenantName": "No JWT Tenant",
  "userId": 1,
  "data": "{\"database\":{\"connectionString\":\"Host=localhost;Database=no_jwt_tenant;...\"}}"
}
```

**Note:** No `jwt` section in `data`

**Step 2: Register User**

```bash
POST https://localhost:5001/api/auth/register
x-tenant-id: no-jwt-tenant

{
  "email": "user@nojwt.com",
  "password": "Test123!",
  "firstName": "No",
  "lastName": "JWT"
}
```

**Step 3: Check Logs**

You should see:

```
[Information] 🔐 Generating JWT token using DEFAULT settings for tenant 'no-jwt-tenant' (tenant has no custom JWT config) (Issuer: IdentityService, Expiry: 60 min)
```

**Step 4: Decode Token**

Payload should use default settings:

```json
{
  "tenant_id": "no-jwt-tenant",
  "iss": "IdentityService",  // ← Default from appsettings.json
  "aud": "MicroservicesApp", // ← Default from appsettings.json
  "exp": ...                 // ← 60 minutes (default)
}
```

---

## Troubleshooting

### Issue: Always Seeing "DEFAULT settings" in Logs

**Possible Causes:**

1. **Multi-tenancy not enabled**

   ```json
   // Check appsettings.json
   "MultiTenancy": {
     "Enabled": false  // ❌ Should be true
   }
   ```

2. **No x-tenant-id header in request**

   ```bash
   # ❌ Missing header
   POST https://localhost:5001/api/auth/login

   # ✅ Include header
   POST https://localhost:5001/api/auth/login
   x-tenant-id: your-tenant-id
   ```

3. **Tenant has no JWT configuration**

   ```sql
   -- Check tenant data in database
   SELECT tenant_id, data FROM tenants WHERE tenant_id = 'your-tenant-id';

   -- Data should contain jwt section:
   -- {"jwt":{"secret":"...","issuer":"..."},...}
   ```

4. **Tenant JWT secret is empty**

   ```json
   // ❌ Empty secret
   {
     "jwt": {
       "secret": "",  // Empty!
       "issuer": "..."
     }
   }

   // ✅ Valid secret
   {
     "jwt": {
       "secret": "valid-secret-key-at-least-32-characters",
       "issuer": "..."
     }
   }
   ```

---

### Issue: Token Validation Fails

**Symptoms:**

```
401 Unauthorized
Token signature is invalid
```

**Possible Causes:**

1. **JwtMode mismatch between token generation and validation**

   **Token Generated:**

   - `JwtMode: Shared` → Uses appsettings.json secret

   **Token Validated:**

   - `JwtMode: PerTenant` → Tries to use tenant secret

   **Solution:** Ensure `JwtMode` setting is consistent

2. **Tenant secret changed after token was issued**

   **Solution:** User needs to log in again to get new token

3. **Wrong tenant header during validation**

   ```bash
   # Token generated for tenant-A
   POST /api/auth/login
   x-tenant-id: tenant-A

   # But validated with tenant-B header
   GET /api/user/profile
   x-tenant-id: tenant-B  # ❌ Wrong tenant!
   Authorization: Bearer {token_from_tenant_A}
   ```

---

## Log Search Patterns

### Find All JWT Token Generations

**PowerShell:**

```powershell
Select-String -Path "C:\...\Logs\Identity\*.log" -Pattern "🔐 Generating JWT"
```

**Output:**

```
identity-2025-10-27.log:45: 🔐 Generating JWT token using TENANT-SPECIFIC settings...
identity-2025-10-27.log:89: 🔐 Generating JWT token using DEFAULT settings...
```

### Find Tenant-Specific Generations Only

```powershell
Select-String -Path "C:\...\Logs\Identity\*.log" -Pattern "TENANT-SPECIFIC settings"
```

### Find Token Validation Issues

```powershell
Select-String -Path "C:\...\Logs\Identity\*.log" -Pattern "Token validation|Invalid token"
```

---

## Quick Verification Checklist

Before testing, verify:

- [ ] Multi-tenancy is enabled in Identity Service (`MultiTenancy.Enabled = true`)
- [ ] Tenant Service is running (`https://localhost:5002`)
- [ ] Tenant exists in Tenant Service database
- [ ] Tenant has JWT configuration (if testing tenant-specific JWT)
- [ ] JWT secret is at least 32 characters
- [ ] x-tenant-id header is included in requests

**Test:**

- [ ] Register/login with tenant header
- [ ] Check logs for "🔐 Generating JWT token using TENANT-SPECIFIC settings"
- [ ] Decode token and verify `tenant_id`, `iss`, `aud` claims
- [ ] Verify token expiration matches tenant configuration

---

## Summary

### Key Log Messages to Watch For

| Log Message                                                                                         | Meaning                                           |
| --------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| `🔐 Generating JWT token using TENANT-SPECIFIC settings for tenant 'xxx'`                           | ✅ Using tenant's custom JWT secret               |
| `🔐 Generating JWT token using DEFAULT settings for tenant 'xxx' (tenant has no custom JWT config)` | ⚠️ Tenant exists but no custom JWT configured     |
| `🔐 Generating JWT token using DEFAULT settings (no tenant context)`                                | ℹ️ No tenant header provided (single-tenant mode) |
| `Added tenant_id claim: xxx`                                                                        | ✅ Tenant ID added to token                       |
| `✅ JWT token generated successfully`                                                               | ✅ Token generation completed                     |

### Expected Behavior by Configuration

| Multi-Tenancy | Tenant Header | Tenant Has JWT Config | Uses                       |
| ------------- | ------------- | --------------------- | -------------------------- |
| Disabled      | -             | -                     | Default (appsettings.json) |
| Enabled       | No            | -                     | Default (appsettings.json) |
| Enabled       | Yes           | No                    | Default (appsettings.json) |
| Enabled       | Yes           | Yes                   | **Tenant-Specific JWT** ✅ |

---

**Last Updated:** October 27, 2025  
**Version:** 1.0.0  
**Status:** ✅ Implemented and Ready for Testing
