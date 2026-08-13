# Phone Verification Login Feature

## Overview

The Identity Service supports phone- and email-verification-code authentication as an alternative to traditional password-based authentication. This feature allows users to register and login using a one-time verification code sent to their phone number or email address, instead of (or in addition to) a password.

## Architecture

### Components

1. **OTP Service (Shared)** - Located in `IhsanDev.Shared.Infrastructure/Services/Otp/`

   - `IOtpService` - Service interface for generating verification codes (accepts an `OtpSettings` parameter)
   - `OtpService` - Configurable implementation with:
     - Cryptographically secure random generation
     - Support for numeric-only or alphanumeric codes
     - Configurable code length (default: 6 digits if no `OtpSettings` is supplied — see `OtpService.GenerateCode`)
   - `IExternalOtpProvider` - Interface for external OTP providers (e.g., Twilio, AWS SNS)

2. **Shared Kernel** - Located in `IhsanDev.Shared.Kernel/`

   - `TenantConfiguration.OtpSettings` - Tenant-specific OTP configuration:
     - `CodeLength` - Length of generated code (default: 6)
     - `ExpirationSeconds` - Code validity duration (default: 300 = 5 minutes)
     - `MaxAttempts` - Maximum failed attempts before lockout (default: 3)
     - `LockoutMinutes` - Lockout duration after max attempts (default: 15)
     - `ResendCooldownSeconds` - Cooldown between code requests (default: 60)
     - `UseAlphanumeric` - Generate alphanumeric vs numeric codes (default: false)
     - `SecretKey` - Optional encryption key for OTP operations

3. **Domain Changes** - `Identity.Domain/Entities/User.cs`

   - `VerificationCode` (string, nullable) - The generated OTP code
   - `VerificationCodeExpiry` (DateTime, nullable) - When the code expires
   - `FailedCodeAttempts` (int, default 0) - Count of failed verification attempts
   - `CodeLockoutUntil` (DateTime, nullable) - Account lockout end time after max failed attempts
   - `LastCodeSentAt` (DateTime, nullable) - Timestamp of last code generation (for cooldown enforcement)

4. **Application Layer**

   - Six commands with validators in `Identity.Application/Commands/Auth/`:
     - `GetVerificationCodeByPhoneCommand` / `GetVerificationCodeByEmailCommand`
     - `LoginWithCodeByPhoneCommand` / `LoginWithCodeByEmailCommand`
     - `RegisterWithCodeByPhoneCommand` / `RegisterWithCodeByEmailCommand`
   - Six handlers in `Identity.Application/Handlers/Auth/`:
     - `GetVerificationCodeByPhoneCommandHandler` / `GetVerificationCodeByEmailCommandHandler`
     - `LoginWithCodeByPhoneCommandHandler` / `LoginWithCodeByEmailCommandHandler`
     - `RegisterWithCodeByPhoneCommandHandler` / `RegisterWithCodeByEmailCommandHandler`
   - **All handlers include:**
     - `GetOtpSettings()` helper method for multi-tenant OTP configuration
     - Security logic (expiration, attempts, lockout, cooldown)
     - Fallback to appsettings.json when tenant has no custom OTP settings

5. **Infrastructure Layer**

   - `ConfigurationHelper.GetOtpSettings()` - Centralized OTP configuration resolution:
     - Checks `MultiTenancy:Enabled` setting
     - Returns tenant-specific OTP settings if available
     - Falls back to `OtpSettings` section in appsettings.json
     - Follows same pattern as `GetJwtSettings()` and `GetDatabaseConnectionString()`

6. **API Layer**
   - Six endpoints, all under the versioned `/api/v{version:apiVersion}/auth/` route group (`Identity.API/Extensions/EndpointMappingExtensions.cs`, `MapAuthEndpoints`) — currently version 1, i.e. `/api/v1/auth/...`:
     - `POST /api/v1/auth/get-verification-code-by-phone`
     - `POST /api/v1/auth/get-verification-code-by-email`
     - `POST /api/v1/auth/login-with-code-by-phone`
     - `POST /api/v1/auth/login-with-code-by-email`
     - `POST /api/v1/auth/register-with-code-by-phone`
     - `POST /api/v1/auth/register-with-code-by-email`
   - The whole `/auth` group carries `[OptionalTenant]` (x-tenant-id header optional) and is rate-limited via the `"PerTenant"` policy (not `"PerUser"` — there is no authenticated user yet at this point in the flow).

## API Endpoints

All six endpoints live under `/api/v1/auth/`. Phone and email are separate, mutually exclusive endpoint pairs — there is no single generic "get code" / "login with code" / "register with code" endpoint.

### 1. Get Verification Code by Phone

**Endpoint:** `POST /api/v1/auth/get-verification-code-by-phone`

**Description:** Generates and saves a verification code (default 6 digits, configurable) for an existing user's phone number.

**Request Body** (`GetVerificationCodeByPhoneCommand`):

```json
{
  "phoneNumber": "+1234567890"
}
```

**Response** (`VerificationCodeResponseDto`, 200):

```json
{
  "success": true,
  "code": "123456",
  "message": null
}
```

- `code` is only populated when the API is running in the **Development** environment (`IHostEnvironment.IsDevelopment()`); in any other environment `code` is always `null`.
- `message` is defined on the DTO but is never set by the current handler — it is always `null`.

**Validation:**

- Phone number is required
- Must match pattern: `^\+?[1-9]\d{1,14}$`

**Behavior for an unknown phone number (anti-enumeration):**

The handler does **not** return `404 Not Found`. If no user has that phone number, it returns the same `200 OK` shape as a successful call — `{ "success": true, "code": null, "message": null }` — so a caller cannot use this endpoint to probe which phone numbers are registered (same pattern as `ForgetPasswordCommandHandler`). See `GetVerificationCodeByPhoneCommandHandler.cs` and the corresponding test `GetVerificationCodeByPhone_WithNonExistentPhone_ShouldReturnGenericSuccess`.

**Error Cases:**

- `403 Forbidden` - Account is disabled (`"Account is disabled. Please contact support"`)
- `403 Forbidden` - Account is locked out from too many failed code attempts (`"Account is temporarily locked due to too many failed attempts. Please try again in {n} minute(s)."`)
- `400 Bad Request` - Still within the resend cooldown window (`"Please wait {n} second(s) before requesting a new code."`)
- `400 Bad Request` - Invalid phone number format (validation failure)

---

### 2. Get Verification Code by Email

**Endpoint:** `POST /api/v1/auth/get-verification-code-by-email`

**Description:** Identical behavior to the phone variant above, keyed by email address instead.

**Request Body** (`GetVerificationCodeByEmailCommand`):

```json
{
  "email": "user@example.com"
}
```

**Response:** Same `VerificationCodeResponseDto` shape as endpoint 1.

**Validation:**

- Email is required and must be a valid email address

**Behavior for an unknown email:** Same anti-enumeration behavior as the phone variant — returns `200 OK` with `{ "success": true, "code": null }`, never `404`.

**Error Cases:** Same as endpoint 1 (`403` disabled/locked-out, `400` cooldown/invalid format).

---

### 3. Login with Verification Code by Phone

**Endpoint:** `POST /api/v1/auth/login-with-code-by-phone`

**Description:** Authenticates a user with a phone number and verification code, returning JWT tokens.

**Request Body** (`LoginWithCodeByPhoneCommand`):

```json
{
  "phoneNumber": "+1234567890",
  "verificationCode": "123456"
}
```

**Response** (`UserDtoIncludesToken`, 200):

```json
{
  "id": 1,
  "isArchived": false,
  "status": true,
  "created": "2025-10-30T10:00:00Z",
  "createdBy": null,
  "lastModified": "2025-10-30T12:00:00Z",
  "lastModifiedBy": null,
  "email": null,
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "emailConfirmed": false,
  "lastLogin": "2025-10-30T12:00:00Z",
  "roles": [],
  "profilePictureId": null,
  "profilePicture": null,
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64-encoded-refresh-token",
  "refreshTokenExpiryTime": "2025-11-06T12:00:00Z",
  "data": null
}
```

`UserDtoIncludesToken` extends the shared `BaseUserDto`/`BaseDto` — there is **no** `role`/`roleName` singular field and **no** `profilePictureUrl` field. The real shape has a `roles` array (only populated for SuperAdmin/Admin-initiated flows — always `[]` for a normal self-login) and separate `profilePictureId` (`int?`) / `profilePicture` (`FileManagerDto?`) fields. `passwordHash` exists on the base DTO but is `[JsonIgnore]`d and never serialized.

**Validation:**

- Phone number is required and must be valid format
- Verification code is required and must exactly match the configured `CodeLength` (**6 digits by default**, configurable per-tenant or via `OtpSettings:CodeLength` — not a fixed 5-digit code), and must be all-digits (or alphanumeric if `UseAlphanumeric` is enabled)

**Security:**

- Verification code and all OTP security fields (`VerificationCodeExpiry`, `FailedCodeAttempts`, `CodeLockoutUntil`) are cleared after a successful login
- A wrong code does **not** return a fixed, purely generic message — the error text is dynamic and includes the number of attempts remaining before lockout: `"Phone number or verification code is incorrect. {remainingAttempts} attempt(s) remaining."`
- Once `MaxAttempts` is reached, the account is locked and the response becomes `403 Forbidden`: `"Too many failed attempts. Account is locked for {LockoutMinutes} minute(s)."`
- An unknown phone number returns the same `401 Unauthorized` as a wrong code (`"Invalid email or password"` — the shared `InvalidCredentials` message is reused here even though the flow is phone/code, not email/password) — it does not reveal whether the phone number itself exists

**Error Cases:**

- `401 Unauthorized` - Unknown phone number, wrong verification code, or expired code (`otp_code_expired`: `"Verification code has expired. Please request a new one"`)
- `403 Forbidden` - Account is disabled, or account is locked out (max attempts exceeded)
- `400 Bad Request` - Invalid format (wrong code length, non-numeric code, invalid phone format)

---

### 4. Login with Verification Code by Email

**Endpoint:** `POST /api/v1/auth/login-with-code-by-email`

**Description:** Identical behavior to endpoint 3, keyed by email instead of phone.

**Request Body** (`LoginWithCodeByEmailCommand`):

```json
{
  "email": "user@example.com",
  "verificationCode": "123456"
}
```

**Response:** Same `UserDtoIncludesToken` shape as endpoint 3 (with `email` populated and `phoneNumber` typically `null` for an email-only account).

**Security:** Same dynamic error messages as endpoint 3, but the incorrect-code message reads `"Email or verification code is incorrect. {remainingAttempts} attempt(s) remaining."`

**Error Cases:** Same as endpoint 3.

---

### 5. Register with Verification Code by Phone

**Endpoint:** `POST /api/v1/auth/register-with-code-by-phone`

**Description:** Creates a new user account with **no password and no email**, identified purely by phone number.

**Request Body** (`RegisterWithCodeByPhoneCommand`):

```json
{
  "phoneNumber": "+1234567890",
  "firstName": "John",
  "lastName": "Doe",
  "data": null
}
```

There is no `email` field on this command — phone and email registration are mutually exclusive endpoints, not a shared body with optional fields. `data` is an optional free-form string (e.g. JSON metadata from a mobile client) stored as-is on the user.

**Response:** Same `VerificationCodeResponseDto` shape as endpoint 1 — `{ "success": true, "code": "<code, dev only>", "message": null }`. This is **not** a plain `{ "success": true, "message": "Registration successful..." }` object; it reuses the same DTO as the "get code" endpoints and can return the freshly generated code in Development.

**Validation:**

- Phone Number: Required, must match `^\+?[1-9]\d{1,14}$`
- First Name: Required, letters/spaces only (`^[a-zA-Z\s]+$`), max 100 characters
- Last Name: Required, letters/spaces only, max 100 characters

**Post-Registration Flow:**

1. User account is created with `PasswordHash = null` and `Email = null`
2. A verification code is generated and saved directly on the new user (no separate "request code" call needed)
3. User must call `/api/v1/auth/login-with-code-by-phone` to authenticate

**Error Cases:**

- `409 Conflict` - Phone number already registered (`"Phone number is already registered"`)
- `400 Bad Request` - Validation errors

---

### 6. Register with Verification Code by Email

**Endpoint:** `POST /api/v1/auth/register-with-code-by-email`

**Description:** Creates a new user account with **no password and no phone number**, identified purely by email.

**Request Body** (`RegisterWithCodeByEmailCommand`):

```json
{
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "data": null
}
```

No `phoneNumber` field — again, a separate, mutually exclusive command from the phone variant.

**Response:** Same `VerificationCodeResponseDto` shape as endpoint 1.

**Validation:**

- Email: Required, valid email format, max 256 characters
- First Name / Last Name: Same rules as endpoint 5

**Error Cases:**

- `409 Conflict` - Email already registered (`"Email address already exists"`)
- `400 Bad Request` - Validation errors

---

## Authentication Flow

### New User Registration Flow (phone variant — email variant is identical, substituting the `-by-email` endpoints)

```
1. User → POST /api/v1/auth/register-with-code-by-phone
   ↓
2. System creates user (no password, no email)
   ↓
3. System generates verification code, saves it directly on the new user
   ↓
4. User → POST /api/v1/auth/login-with-code-by-phone
   ↓
5. System verifies code
   ↓
6. Returns JWT tokens (UserDtoIncludesToken)
```

### Existing User Login Flow (phone variant — email variant is identical, substituting the `-by-email` endpoints)

```
1. User → POST /api/v1/auth/get-verification-code-by-phone
   ↓
2. System checks phone exists (returns generic 200 success either way — no 404)
   ↓
3. System generates & saves code (existing users only)
   ↓
4. User → POST /api/v1/auth/login-with-code-by-phone
   ↓
5. System verifies code
   ↓
6. Code cleared from database
   ↓
7. Returns JWT tokens
```

## OTP Service Implementation

### Default Implementation (Internal)

The default `OtpService` uses cryptographically secure random number generation:

```csharp
var code = _otpService.GenerateCode(otpSettings); // otpSettings.CodeLength controls the length (default 6 if no settings passed)
```

**Features:**

- Uses `RandomNumberGenerator` for security
- Configurable length via `OtpSettings.CodeLength` (defaults to 6 when no settings object is supplied at all)
- No external dependencies

### External Provider Integration

To integrate with external SMS/OTP providers (Twilio, AWS SNS, etc.):

1. **Implement IExternalOtpProvider:**

```csharp
public class TwilioOtpProvider : IExternalOtpProvider
{
    private readonly TwilioRestClient _client;

    public async Task<string> SendOtpAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var code = GenerateSecureCode();

        // Send via Twilio
        await MessageResource.CreateAsync(
            to: new PhoneNumber(phoneNumber),
            from: new PhoneNumber(_fromNumber),
            body: $"Your verification code is: {code}"
        );

        return code;
    }
}
```

2. **Register in DI Container:**

```csharp
// In Program.cs or InfrastructureServiceExtensions.cs
services.AddScoped<IExternalOtpProvider, TwilioOtpProvider>();
services.AddScoped<IOtpService, OtpService>();
```

3. **Usage:**

```csharp
// Automatically uses external provider if registered
var code = await _otpService.GenerateCodeWithExternalProviderAsync(
    phoneNumber,
    cancellationToken
);
```

> **Note:** SMS/email delivery is not wired up yet — every handler above has a `// TODO: Send code via SMS/Email` comment. The generated code is currently only ever surfaced back to the caller in the Development environment's response body; there is no production delivery path until an `IExternalOtpProvider` is implemented and registered.

## Database Changes

The OTP columns are **not** the product of a standalone incremental migration in the current codebase — they are part of the `Users` table definition in the current baseline migration, `20260606180309_InitialCreate` (`Identity.Infrastructure/Migrations/`). The migration history was squashed/reset since this feature was first introduced, so there is no separate `AddVerificationCodeToUser`/`UpdateOtpSecurityFields` migration to point to anymore — check `Identity.Infrastructure/Migrations/` for the actual current migration set before referencing migration names in any future doc update.

**Columns on `Users` (all defined in `InitialCreate`):**

- **VerificationCode** (`text`, nullable) - Stores the temporary verification code
- **VerificationCodeExpiry** (`timestamp with time zone`, nullable)

  - Tracks when the OTP code expires
  - Default expiration: 5 minutes after generation (configurable via `OtpSettings:ExpirationSeconds`)
  - Codes rejected if current time > expiration time

- **FailedCodeAttempts** (`integer`, not null, default 0)

  - Counts failed verification attempts
  - Increments on incorrect code submission
  - Resets to 0 when a new code is generated

- **CodeLockoutUntil** (`timestamp with time zone`, nullable)

  - Records when account lockout ends
  - Set to (now + LockoutMinutes) when MaxAttempts exceeded
  - All OTP operations blocked while locked out

- **LastCodeSentAt** (`timestamp with time zone`, nullable)
  - Timestamp of last code generation
  - Used to enforce resend cooldown
  - Prevents code request spam

Later migrations present in the same folder (`20260730120000_AddPasswordLoginLockout`, `20260809180000_AddIsSystemClaim`) are unrelated to OTP — they add password-login lockout fields and a claim-catalog flag, respectively.

**Apply Migrations:**

```bash
cd src/Services/Identity/Identity.Infrastructure
dotnet ef database update --startup-project ../Identity.API
```

**For Multi-Tenant Databases:**

If using database-per-tenant architecture, migrations must be applied to each tenant database. In practice this project no longer requires the manual loop below for day-to-day operation — new tenants are eagerly migrated via the Layer-3 `tenant:provisioned` Redis Pub/Sub listener (`AddTenantProvisioningListener<IdentityDbContext>`, see `Doc/AUTOMATIC_DATABASE_MIGRATION.md`) and `UseTenantDatabaseMigration` handles any tenant lazily on its first request. Manual iteration remains useful for a one-off backfill:

```csharp
var tenants = await _tenantService.GetAllActiveTenantsAsync();
foreach (var tenant in tenants)
{
    if (tenant.Configuration?.Database?.ConnectionString != null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(tenant.Configuration.Database.ConnectionString);

        using var dbContext = new IdentityDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();
    }
}
```

## Security Considerations

### Implemented Security Features ✅

1. **Code Generation Security:**

   - Cryptographically secure random number generation (`RandomNumberGenerator`)
   - Configurable code length (default: 6 digits = 1,000,000 combinations)
   - Support for alphanumeric codes (36^6 = 2+ billion combinations)
   - Codes cleared after successful login

2. **Code Expiration:**

   - All codes expire after configurable time (default: 5 minutes)
   - Expired codes automatically rejected during login
   - Expiration tracked in `VerificationCodeExpiry` field
   - **Security benefit:** Reduces window for brute-force attacks

3. **Failed Attempt Tracking:**

   - System tracks failed verification attempts per user
   - Default: 3 failed attempts before account lockout
   - `FailedCodeAttempts` counter increments on each failed attempt
   - Counter resets to 0 when new code is generated
   - **Security benefit:** Prevents brute-force code guessing

4. **Account Lockout:**

   - After max failed attempts, account is locked for configurable duration (default: 15 minutes)
   - Lockout tracked in `CodeLockoutUntil` field (DateTime)
   - All OTP operations blocked during lockout (get code, login)
   - **Security benefit:** Forces attackers to wait, making brute-force impractical

5. **Resend Cooldown:**

   - Minimum time between code requests (default: 60 seconds)
   - Tracked via `LastCodeSentAt` field
   - Prevents code request spam
   - **Security benefit:** Prevents denial-of-service attacks via SMS flooding

6. **Error Handling:**

   - Unknown phone/email on "get code" is never distinguishable from a known one — both return `200 OK` with the same generic success shape (no `404`)
   - A wrong code on login **does** intentionally leak one piece of dynamic state — the number of attempts remaining (`"...{remainingAttempts} attempt(s) remaining."`) — and the lockout message includes the exact lockout duration. This is a deliberate UX trade-off (helping a legitimate user know how many tries they have left) at the cost of leaking attempt-count state to an attacker; it is not a purely generic, static message
   - Example incorrect-code message: `"Phone number or verification code is incorrect. 2 attempt(s) remaining."`
   - **Security benefit:** Prevents phone/email-existence enumeration on the "get code" step; the login step still does not reveal whether the phone/email itself is registered (unknown identifier and wrong code both return the same `401`)

7. **Account Status Validation:**
   - Status checks (disabled accounts cannot get codes or login)
   - Last login timestamp updated on successful authentication
   - Supports dual authentication (users can choose password OR code)

### Security Configuration

**Default Security Settings (Recommended for Production):**

```json
{
  "OtpSettings": {
    "CodeLength": 6,
    "ExpirationSeconds": 300,
    "MaxAttempts": 3,
    "LockoutMinutes": 15,
    "ResendCooldownSeconds": 60,
    "UseAlphanumeric": false
  }
}
```

**High-Security Profile (Enterprise/Banking):**

```json
{
  "OtpSettings": {
    "CodeLength": 8,
    "ExpirationSeconds": 180,
    "MaxAttempts": 2,
    "LockoutMinutes": 30,
    "ResendCooldownSeconds": 120,
    "UseAlphanumeric": true
  }
}
```

**Development/Testing Profile:**

```json
{
  "OtpSettings": {
    "CodeLength": 4,
    "ExpirationSeconds": 600,
    "MaxAttempts": 10,
    "LockoutMinutes": 1,
    "ResendCooldownSeconds": 10,
    "UseAlphanumeric": false
  }
}
```

### Additional Production Recommendations

1. **✅ IMPLEMENTED - Rate Limiting:**

   - ✅ Failed attempt tracking (3 attempts default)
   - ✅ Account lockout (15 minutes default)
   - ✅ Resend cooldown (60 seconds default)
   - ✅ The entire `/api/v1/auth` group is also covered by the endpoint-level `"PerTenant"` rate-limiting policy (credential-stuffing/brute-force flood mitigation), independent of the per-user OTP lockout above
   - 🔜 **Optional:** IP-based rate limiting via middleware

2. **✅ IMPLEMENTED - Code Expiration:**

   - ✅ Timestamp tracking (`VerificationCodeExpiry`)
   - ✅ Automatic expiration validation
   - ✅ Configurable expiration time (5 minutes default)
   - 🔜 **Optional:** Background job to clean up expired codes

3. **🔜 SMS Delivery Integration:**

   - Implement external OTP provider (Twilio, AWS SNS)
   - Log SMS delivery status
   - Handle delivery failures gracefully
   - Retry logic for failed deliveries

4. **🔜 Enhanced Audit Logging:**
   - Log all verification code generations with user/tenant context
   - Log successful/failed login attempts with timestamps
   - Monitor for suspicious patterns (e.g., rapid failed attempts)
   - Dashboard for security analytics

## Integration with Existing Features

### Compatibility

- ✅ Works alongside password-based authentication
- ✅ JWT token generation unchanged
- ✅ Refresh token mechanism compatible
- ✅ Role-based authorization works normally
- ✅ Multi-tenancy support maintained

### User Types

**Users with Password:** Can use either:

- Email + Password (existing flow)
- Phone + Verification Code, or Email + Verification Code (new flow)

**Users without Password (code-only):**

- Must use Phone + Verification Code, or Email + Verification Code
- Created via `RegisterWithCodeByPhoneCommand` / `RegisterWithCodeByEmailCommand`

## Testing

### Manual Testing with Swagger

1. **Register User with Code (phone):**

```bash
POST /api/v1/auth/register-with-code-by-phone
{
  "phoneNumber": "+1234567890",
  "firstName": "Test",
  "lastName": "User"
}
```

2. **Check Database for Code:**

```sql
SELECT "VerificationCode" FROM "Users" WHERE "PhoneNumber" = '+1234567890';
```

3. **Login with Code:**

```bash
POST /api/v1/auth/login-with-code-by-phone
{
  "phoneNumber": "+1234567890",
  "verificationCode": "[CODE_FROM_DB]"
}
```

(Substitute the `-by-email` endpoints and an `email`/`firstName`/`lastName` body for the email flow.)

### Automated Tests — Already Implemented

All of the OTP flows above are covered by integration tests in `Identity.API.Tests/Endpoints/OtpAuthEndpointsTests.cs` — this is not pending/TODO work. The file currently covers, across all 6 endpoints:

- Valid phone/email → success, code persisted with the configured length, expiry set
- Unknown phone/email → generic `200` success with `code: null` (anti-enumeration)
- Invalid phone/email format → validation exception
- Disabled account → `ForbiddenException` ("disabled")
- Locked-out account → `ForbiddenException` ("locked")
- Within resend cooldown → `BadRequestException` ("wait")
- Valid login code → tokens returned, code/lockout fields cleared
- Wrong code → `UnauthorizedException` ("incorrect"/"remaining"), failed-attempt counter incremented
- Max failed attempts reached → `ForbiddenException` ("Too many failed attempts"/"locked"), `CodeLockoutUntil` set
- Expired code → `UnauthorizedException` ("expired")
- Wrong code length / non-numeric code → validation exception
- Duplicate phone/email on register → `ConflictException`
- `Data` metadata field round-trips through registration
- End-to-end flows: register → login, and get-code → login

When adding a new OTP-related behavior, extend this file rather than treating OTP test coverage as a future task.

## Configuration

### Global Configuration (appsettings.json)

Add `OtpSettings` section to your Identity Service appsettings:

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
  },
  "Jwt": {
    "Secret": "your-secret-key",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp"
  },
  "DatabaseSettings": {
    "ConnectionString": "Host=localhost;Database=identity;Username=postgres;Password=postgres"
  }
}
```

### Multi-Tenant Configuration

When multi-tenancy is enabled (`MultiTenancy:Enabled = true`), OTP settings can be customized per tenant.

**Enable Multi-Tenancy:**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5002",
    "CacheExpirationMinutes": 5
  }
}
```

**Tenant-Specific OTP Settings (stored in Tenant Service):**

```json
{
  "tenantId": "acme-corp",
  "tenantName": "Acme Corporation",
  "isActive": true,
  "configuration": {
    "database": {
      "provider": "PostgreSql",
      "connectionString": "Host=tenant-db.acme.com;Database=acme;..."
    },
    "jwt": {
      "secret": "acme-jwt-secret",
      "issuer": "IdentityService",
      "audience": "MicroservicesApp"
    },
    "otp": {
      "codeLength": 8,
      "expirationSeconds": 600,
      "maxAttempts": 5,
      "lockoutMinutes": 30,
      "resendCooldownSeconds": 120,
      "useAlphanumeric": true,
      "secretKey": "acme-otp-encryption-key"
    }
  }
}
```

**Configuration Resolution (Multi-Tenant):**

```
Request with x-tenant-id: acme-corp header
    ↓
1. Middleware extracts tenant ID
    ↓
2. TenantConfigurationProvider fetches tenant config from Tenant Service (cached)
    ↓
3. Handler calls GetOtpSettings()
    ↓
4. Check MultiTenancy:Enabled = true?
    ├─ YES: Check tenant.Configuration.Otp exists?
    │   ├─ YES: Return tenant-specific OTP settings ✅
    │   └─ NO: Fallback to appsettings.json OtpSettings
    └─ NO: Use appsettings.json OtpSettings
    ↓
5. Generate code with resolved settings
```

**Use Cases for Per-Tenant OTP Configuration:**

- **Enterprise Tenant:** Stricter security (8-digit alphanumeric, 2 attempts, 30-minute lockout)
- **Standard Tenant:** Balanced security (6-digit numeric, 3 attempts, 15-minute lockout)
- **Internal Tenant:** Development-friendly (4-digit numeric, 10 attempts, 1-minute lockout)

### External SMS Provider Configuration (Optional)

To integrate with Twilio, AWS SNS, or other SMS providers:

**appsettings.json:**

```json
{
  "TwilioSettings": {
    "AccountSid": "your-twilio-account-sid",
    "AuthToken": "your-twilio-auth-token",
    "FromNumber": "+1234567890"
  }
}
```

**Implementation:**

```csharp
public class TwilioOtpProvider : IExternalOtpProvider
{
    private readonly TwilioRestClient _client;
    private readonly string _fromNumber;
    private readonly IOtpService _otpService;

    public async Task<string> SendOtpAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        // Get OTP settings (respects multi-tenancy)
        var otpSettings = GetOtpSettings();

        // Generate code using tenant/global settings
        var code = _otpService.GenerateCode(otpSettings);

        // Send via Twilio
        await MessageResource.CreateAsync(
            to: new PhoneNumber(phoneNumber),
            from: new PhoneNumber(_fromNumber),
            body: $"Your verification code is: {code}. Valid for {otpSettings.ExpirationSeconds / 60} minutes."
        );

        return code;
    }
}
```

## Future Enhancements

1. **SMS/Email Delivery Integration:**

   - Integrate with Twilio/AWS SNS (SMS) and an email provider
   - Support multiple countries/formats
   - Every handler currently has a `// TODO: Send code via SMS/Email` — this is the main remaining gap, not code expiration/resend limiting, both of which are already implemented (see Security Considerations above)

2. **Two-Factor Authentication:**

   - Use as 2FA option for password users
   - Optional verification code for sensitive operations

3. **Backup Codes:**
   - Generate backup codes during registration
   - Allow login with backup code if phone/email unavailable

## Troubleshooting

### Common Issues

**Issue:** "Phone number or verification code is incorrect. {n} attempt(s) remaining."

- **Cause:** Code doesn't match, or wrong phone/email (the message is intentionally identical either way)
- **Solution:** Request a new code via `/api/v1/auth/get-verification-code-by-phone` (or `-by-email`)

**Issue:** "Verification code has expired. Please request a new one"

- **Cause:** More than `ExpirationSeconds` has passed since the code was generated
- **Solution:** Request a new code via the same "get verification code" endpoint

**Issue:** "Account is disabled. Please contact support"

- **Cause:** User `Status` is `false`
- **Solution:** Admin must enable account

**Issue:** "Phone number is already registered" / "Email address already exists"

- **Cause:** Attempting to register a duplicate phone/email via `register-with-code-by-phone`/`-by-email`
- **Solution:** Use the corresponding `login-with-code-by-*` endpoint instead

**Issue:** Migration fails with multi-tenancy error

- **Cause:** Multi-tenancy checks during design time
- **Solution:** Temporarily disable via environment variable:

```bash
$env:MultiTenancy__Enabled="false"
dotnet ef migrations add [MigrationName]
```

## References

- CQRS Pattern: [MediatR Documentation](https://github.com/jbogard/MediatR)
- JWT Authentication: [Microsoft Docs](https://docs.microsoft.com/en-us/aspnet/core/security/authentication)
- FluentValidation: [Official Documentation](https://docs.fluentvalidation.net/)
- Entity Framework Core: [Migrations Guide](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/)

---

**Version:** 2.0
**Last Updated:** August 13, 2026
**Author:** Identity Service Team
