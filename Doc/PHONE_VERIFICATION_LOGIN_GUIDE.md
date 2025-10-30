# Phone Verification Login Feature

## Overview

The Identity Service now supports phone verification-based authentication as an alternative to traditional password-based authentication. This feature allows users to register and login using a verification code sent to their phone number.

## Architecture

### Components

1. **OTP Service (Shared)** - Located in `IhsanDev.Shared.Infrastructure/Services/Otp/`

   - `IOtpService` - Service interface for generating verification codes (accepts OtpSettings parameter)
   - `OtpService` - Configurable implementation with:
     - Cryptographically secure random generation
     - Support for numeric-only or alphanumeric codes
     - Configurable code length (4-10 digits/characters)
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

   - Six new commands with validators in `Identity.Application/Commands/Auth/`:
     - `GetVerificationCodeByPhoneCommand` / `GetVerificationCodeByEmailCommand`
     - `LoginWithCodeByPhoneCommand` / `LoginWithCodeByEmailCommand`
     - `RegisterWithCodeByPhoneCommand` / `RegisterWithCodeByEmailCommand`
   - Six new handlers in `Identity.Application/Handlers/Auth/`:
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
   - Six new endpoints in `/api/auth`:
     - `POST /api/auth/get-verification-code-by-phone`
     - `POST /api/auth/get-verification-code-by-email`
     - `POST /api/auth/login-with-code-by-phone`
     - `POST /api/auth/login-with-code-by-email`
     - `POST /api/auth/register-with-code-by-phone`
     - `POST /api/auth/register-with-code-by-email`

## API Endpoints

### 1. Get Verification Code

**Endpoint:** `POST /api/auth/get-verification-code`

**Description:** Generates and saves a 5-digit verification code for an existing user's phone number.

**Request Body:**

```json
{
  "phoneNumber": "+1234567890"
}
```

**Response:**

```json
{
  "success": true,
  "message": "Verification code sent successfully"
}
```

**Validation:**

- Phone number is required
- Must match pattern: `^\+?[1-9]\d{1,14}$`

**Error Cases:**

- `404 Not Found` - Phone number not registered
- `403 Forbidden` - Account is disabled
- `400 Bad Request` - Invalid phone number format

---

### 2. Login with Verification Code

**Endpoint:** `POST /api/auth/login-with-code`

**Description:** Authenticates user with phone number and verification code, returning JWT tokens.

**Request Body:**

```json
{
  "phoneNumber": "+1234567890",
  "verificationCode": "12345"
}
```

**Response:**

```json
{
  "id": 1,
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "role": "User",
  "roleName": "User",
  "status": true,
  "emailConfirmed": false,
  "created": "2025-10-30T10:00:00Z",
  "lastModified": "2025-10-30T12:00:00Z",
  "lastLogin": "2025-10-30T12:00:00Z",
  "profilePictureUrl": null,
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64-encoded-refresh-token",
  "refreshTokenExpiryTime": "2025-11-06T12:00:00Z"
}
```

**Validation:**

- Phone number is required and must be valid format
- Verification code is required, must be exactly 5 digits (0-9)

**Security:**

- Verification code is cleared after successful login
- Failed attempts do not reveal whether phone number exists
- Returns generic error: "Phone number or verification code is incorrect"

**Error Cases:**

- `401 Unauthorized` - Invalid phone number or verification code
- `403 Forbidden` - Account is disabled
- `400 Bad Request` - Invalid format

---

### 3. Register with Verification Code

**Endpoint:** `POST /api/auth/register-with-code`

**Description:** Creates a new user account without password, using phone verification.

**Request Body:**

```json
{
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890"
}
```

**Response:**

```json
{
  "success": true,
  "message": "Registration successful. Please login with the verification code sent to your phone."
}
```

**Validation:**

- Email: Required, valid email format, max 256 characters
- First Name: Required, letters only, max 100 characters
- Last Name: Required, letters only, max 100 characters
- Phone Number: Required, valid phone format

**Post-Registration Flow:**

1. User account is created with no password
2. Verification code is generated and saved
3. User must call `/api/auth/login-with-code` to authenticate

**Error Cases:**

- `409 Conflict` - Email or phone number already registered
- `400 Bad Request` - Validation errors

---

## Authentication Flow

### New User Registration Flow

```
1. User → POST /api/auth/register-with-code
   ↓
2. System creates user (no password)
   ↓
3. System generates verification code
   ↓
4. Code saved to database
   ↓
5. User → POST /api/auth/login-with-code
   ↓
6. System verifies code
   ↓
7. Returns JWT tokens
```

### Existing User Login Flow

```
1. User → POST /api/auth/get-verification-code
   ↓
2. System checks phone exists
   ↓
3. System generates & saves code
   ↓
4. User → POST /api/auth/login-with-code
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
var code = _otpService.GenerateCode(5); // Generates 5-digit code
```

**Features:**

- Uses `RandomNumberGenerator` for security
- Configurable length (4-10 digits)
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

## Database Changes

### Migration 1: AddVerificationCodeToUser

**New Column:**

- **Table:** Users
- **Column:** VerificationCode
- **Type:** varchar/text (nullable)
- **Purpose:** Stores temporary verification codes

### Migration 2: UpdateOtpSecurityFields (20251030154411)

**New Columns:**

- **VerificationCodeExpiry** (DateTime, nullable)

  - Tracks when the OTP code expires
  - Default expiration: 5 minutes after generation
  - Codes rejected if current time > expiration time

- **FailedCodeAttempts** (int, default 0)

  - Counts failed verification attempts
  - Increments on incorrect code submission
  - Resets to 0 when new code is generated

- **CodeLockoutUntil** (DateTime, nullable)

  - Records when account lockout ends
  - Set to (now + LockoutMinutes) when MaxAttempts exceeded
  - All OTP operations blocked while locked out

- **LastCodeSentAt** (DateTime, nullable)
  - Timestamp of last code generation
  - Used to enforce resend cooldown
  - Prevents code request spam

**Apply Migrations:**

```bash
cd src/Services/Identity/Identity.Infrastructure
dotnet ef database update --startup-project ../Identity.API
```

**For Multi-Tenant Databases:**

If using database-per-tenant architecture, migrations must be applied to each tenant database. Use the tenant migration service or apply manually:

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

   - Generic error messages to prevent enumeration
   - No distinction between invalid phone/email and invalid code
   - Example: "Phone number or verification code is incorrect"
   - **Security benefit:** Attackers cannot determine if phone/email exists

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
- Phone + Verification Code (new flow)

**Users without Password (code-only):**

- Must use Phone + Verification Code
- Created via `RegisterWithCodeCommand`

## Testing

### Manual Testing with Swagger

1. **Register User with Code:**

```bash
POST /api/auth/register-with-code
{
  "email": "test@example.com",
  "firstName": "Test",
  "lastName": "User",
  "phoneNumber": "+1234567890"
}
```

2. **Check Database for Code:**

```sql
SELECT VerificationCode FROM Users WHERE PhoneNumber = '+1234567890';
```

3. **Login with Code:**

```bash
POST /api/auth/login-with-code
{
  "phoneNumber": "+1234567890",
  "verificationCode": "[CODE_FROM_DB]"
}
```

### Test Cases to Implement

```csharp
[Fact]
public async Task GetVerificationCode_ValidPhone_ReturnsSuccess() { }

[Fact]
public async Task GetVerificationCode_NonExistentPhone_ReturnsNotFound() { }

[Fact]
public async Task LoginWithCode_ValidCode_ReturnsTokens() { }

[Fact]
public async Task LoginWithCode_InvalidCode_ReturnsUnauthorized() { }

[Fact]
public async Task LoginWithCode_ExpiredCode_ReturnsUnauthorized() { }

[Fact]
public async Task RegisterWithCode_ValidData_CreatesUser() { }

[Fact]
public async Task RegisterWithCode_DuplicatePhone_ReturnsConflict() { }
```

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

1. **Code Expiration:**

   - Add `VerificationCodeExpiry` datetime field
   - Validate expiration in login handler

2. **Resend Limit:**

   - Track code generation attempts
   - Implement cooldown period

3. **SMS Integration:**

   - Integrate with Twilio/AWS SNS
   - Support multiple countries/formats

4. **Two-Factor Authentication:**

   - Use as 2FA option for password users
   - Optional verification code for sensitive operations

5. **Backup Codes:**
   - Generate backup codes during registration
   - Allow login with backup code if phone unavailable

## Troubleshooting

### Common Issues

**Issue:** "Phone number or verification code is incorrect"

- **Cause:** Code doesn't match or expired
- **Solution:** Request new code via `/get-verification-code`

**Issue:** "Account is disabled"

- **Cause:** User status is false
- **Solution:** Admin must enable account

**Issue:** "Phone number is already registered"

- **Cause:** Attempting to register duplicate phone
- **Solution:** Use login endpoint instead

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

**Version:** 1.0  
**Last Updated:** October 30, 2025  
**Author:** Identity Service Team
