# 🧪 CORS Testing Guide

## Quick Testing Methods

### Method 1: Browser-Based Testing (Easiest)

1. **Start the services:**

   ```cmd
   cd src\Services\Identity\Identity.API
   dotnet run
   ```

   ```cmd
   cd src\Services\Tenant\Tenant.API
   dotnet run
   ```

2. **Serve the test HTML file:**

   ```cmd
   cd c:\Users\YOUR_USERNAME\Desktop\Projects\MicroservicesArchitecture
   python -m http.server 4200
   ```

   Or use any web server. The key is to serve it from `http://localhost:4200` to match your CORS config.

3. **Open in browser:**

   ```
   http://localhost:4200/test-cors-simple.html
   ```

4. **Test scenarios:**
   - Click "Test CORS (No Tenant)" - Should work if `http://localhost:4200` is in appsettings.json
   - Click "Test CORS (With Tenant)" - Needs tenant created first
   - Check browser console (F12) for detailed CORS errors

---

### Method 2: PowerShell Script Testing

1. **Run the PowerShell script:**

   ```powershell
   cd c:\Users\YOUR_USERNAME\Desktop\Projects\MicroservicesArchitecture
   .\test-cors.ps1
   ```

2. **The script tests:**
   - Default CORS without tenant
   - Invalid origin rejection
   - Tenant-specific CORS (if tenant exists)

---

### Method 3: Manual cURL Testing

#### Test 1: Default CORS (No Tenant)

```bash
curl -X OPTIONS "https://localhost:5101/api/auth/login" \
  -H "Origin: http://localhost:4200" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: content-type" \
  -v -k
```

**Expected Response:**

```
< HTTP/1.1 204 No Content
< Access-Control-Allow-Origin: http://localhost:4200
< Access-Control-Allow-Credentials: true
< Access-Control-Allow-Methods: GET, POST, PUT, DELETE, PATCH, OPTIONS
< Access-Control-Allow-Headers: content-type
< Access-Control-Max-Age: 86400
```

#### Test 2: With Tenant Header

```bash
curl -X OPTIONS "https://localhost:5101/api/auth/login" \
  -H "Origin: https://tenant-app.com" \
  -H "x-tenant-id: test-tenant" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: content-type" \
  -v -k
```

#### Test 3: Invalid Origin (Should Fail)

```bash
curl -X OPTIONS "https://localhost:5101/api/auth/login" \
  -H "Origin: https://malicious-site.com" \
  -H "Access-Control-Request-Method: POST" \
  -v -k
```

**Expected:** HTTP 403 Forbidden with message "CORS policy: Origin not allowed"

---

## Setting Up Test Tenant

### Step 1: Create a Test Tenant

1. **Start services and get admin token:**

   - Register/Login as admin in Identity Service
   - Copy the JWT token

2. **Open Tenant Service Swagger:**

   ```
   http://localhost:5002/swagger
   ```

3. **Use POST /api/admin/tenant endpoint:**

**Request Body:**

```json
{
  "tenantId": "test-tenant",
  "tenantName": "Test Tenant",
  "userId": 1,
  "startDate": "2025-10-28T00:00:00Z",
  "expireDate": "2026-10-28T00:00:00Z",
  "data": "{\"Cors\":{\"AllowedOrigins\":[\"https://tenant-app.com\",\"http://localhost:4200\"]},\"Database\":{\"Provider\":\"PostgreSql\",\"ConnectionString\":\"Host=localhost;Port=5432;Database=tenant_test;Username=postgres;Password=CHANGE_ME_DB_PASSWORD;\"},\"Jwt\":{\"Secret\":\"test-tenant-secret-key-minimum-32-characters-long\",\"Issuer\":\"TestTenant\",\"Audience\":\"TestApp\",\"AccessTokenExpirationMinutes\":60}}"
}
```

### Step 2: Verify Tenant Configuration

```bash
curl http://localhost:5002/api/tenant/config/test-tenant
```

**Expected Response:**

```json
{
  "tenantId": "test-tenant",
  "tenantName": "Test Tenant",
  "configuration": {
    "cors": {
      "allowedOrigins": [
        "https://tenant-app.com",
        "http://localhost:4200"
      ]
    },
    "database": { ... },
    "jwt": { ... }
  }
}
```

---

## Testing Scenarios

### ✅ Scenario 1: Default CORS (No Tenant)

**Setup:**

- Request WITHOUT `x-tenant-id` header
- Origin: `http://localhost:4200` (from appsettings.json)

**Expected:**

- ✓ CORS allowed
- Uses `appsettings.json` → `Cors:AllowedOrigins`

---

### ✅ Scenario 2: Tenant-Specific CORS

**Setup:**

- Request WITH `x-tenant-id: test-tenant` header
- Origin: `https://tenant-app.com` (from tenant config)

**Expected:**

- ✓ CORS allowed
- Uses tenant's `AllowedOrigins`
- Ignores `appsettings.json` CORS

---

### ✅ Scenario 3: Invalid Origin Rejected

**Setup:**

- Request WITH `x-tenant-id: test-tenant` header
- Origin: `http://localhost:4200` (NOT in tenant's allowed origins)

**Expected:**

- ✗ CORS blocked
- No `Access-Control-Allow-Origin` header
- Browser blocks the response

---

### ✅ Scenario 4: Tenant Without CORS Config

**Setup:**

- Request WITH `x-tenant-id: tenant-no-cors` header
- Origin: `http://localhost:4200`
- Tenant doesn't have CORS configuration

**Expected:**

- ✓ CORS allowed (falls back to appsettings.json)
- Uses `appsettings.json` → `Cors:AllowedOrigins`

---

### ✅ Scenario 5: Multi-Tenancy Disabled

**Setup:**

- `appsettings.json`: `MultiTenancy:Enabled = false`
- Request WITH `x-tenant-id` header

**Expected:**

- Tenant header ignored
- Always uses `appsettings.json` CORS

---

## Checking Results

### Browser Console (F12)

**Success:**

```
(no CORS errors)
```

**Failure:**

```
Access to fetch at 'https://localhost:5101/api/auth/login' from origin
'http://localhost:4200' has been blocked by CORS policy: No
'Access-Control-Allow-Origin' header is present on the requested resource.
```

### Response Headers

**Success:**

```
Access-Control-Allow-Origin: http://localhost:4200
Access-Control-Allow-Credentials: true
Access-Control-Allow-Methods: GET, POST, PUT, DELETE, PATCH, OPTIONS
Access-Control-Allow-Headers: content-type, authorization, x-tenant-id
Access-Control-Max-Age: 86400
```

**Failure (Preflight):**

```
HTTP/1.1 403 Forbidden
CORS policy: Origin not allowed
```

**Failure (Actual Request):**

```
(No CORS headers present - browser blocks response)
```

---

## Troubleshooting

### Issue: "CORS error even though origin is in appsettings.json"

**Check:**

1. Is multi-tenancy enabled?
2. Are you sending `x-tenant-id` header?
3. Does tenant have its own CORS config?

**Solution:**
Either add origin to tenant's CORS config or remove `x-tenant-id` header.

---

### Issue: "Tenant CORS not working"

**Check:**

1. Is Tenant Service running?
2. Does tenant exist? (`GET /api/tenant/config/{tenantId}`)
3. Is tenant active?
4. Is origin exactly matching (including http/https, port)?

---

### Issue: "Middleware order error"

**Verify middleware order in Program.cs:**

```csharp
app.UseTenantResolution(builder.Configuration);  // 1st - Resolves tenant
app.UseTenantAwareCors();                        // 2nd - Validates CORS
// Note: Do NOT use app.UseCors() - not needed!
```

---

### Issue: "Request header field content-type is not allowed"

**Cause:** Using wildcard `*` for headers doesn't work with credentials.

**Solution:** Already fixed! Middleware now echoes back the requested headers from preflight request.

---

## Quick Commands

**Start Identity Service:**

```cmd
cd src\Services\Identity\Identity.API
dotnet run
```

**Start Tenant Service:**

```cmd
cd src\Services\Tenant\Tenant.API
dotnet run
```

**Serve Test HTML:**

```cmd
python -m http.server 4200
```

**Test with cURL:**

```bash
curl -X POST "https://localhost:5101/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:4200" \
  -H "x-tenant-id: test-tenant" \
  -d '{"email":"test@example.com","password":"Test123!"}' \
  -v -k
```

---

## Expected Outcomes Summary

| Scenario     | Tenant Header  | Origin         | Expected Result           |
| ------------ | -------------- | -------------- | ------------------------- |
| Default CORS | No             | localhost:4200 | ✓ Allowed (appsettings)   |
| Default CORS | No             | malicious.com  | ✗ Blocked                 |
| Tenant CORS  | test-tenant    | tenant-app.com | ✓ Allowed (tenant config) |
| Tenant CORS  | test-tenant    | localhost:4200 | ✗ Blocked (not in tenant) |
| Fallback     | tenant-no-cors | localhost:4200 | ✓ Allowed (appsettings)   |
| Disabled     | test-tenant    | localhost:4200 | ✓ Allowed (appsettings)   |

---

**Happy Testing! 🎉**
