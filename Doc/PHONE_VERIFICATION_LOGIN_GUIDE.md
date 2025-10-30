# Phone Verification Login Feature

## Overview

The Identity Service now supports phone verification-based authentication as an alternative to traditional password-based authentication. This feature allows users to register and login using a verification code sent to their phone number.

## Architecture

### Components

1. **OTP Service (Shared)** - Located in `IhsanDev.Shared.Infrastructure/Services/Otp/`

   - `IOtpService` - Service interface for generating verification codes
   - `OtpService` - Default implementation with internal random number generation
   - `IExternalOtpProvider` - Interface for external OTP providers (e.g., Twilio, AWS SNS)

2. **Domain Changes**

   - Added `VerificationCode` property to `User` entity (nullable string)

3. **Application Layer**

   - Three new commands with validators in `Identity.Application/Commands/Auth/`:
     - `GetVerificationCodeCommand`
     - `LoginWithCodeCommand`
     - `RegisterWithCodeCommand`
   - Three new handlers in `Identity.Application/Handlers/Auth/`:
     - `GetVerificationCodeCommandHandler`
     - `LoginWithCodeCommandHandler`
     - `RegisterWithCodeCommandHandler`

4. **API Layer**
   - Three new endpoints in `/api/auth`:
     - `POST /api/auth/get-verification-code`
     - `POST /api/auth/login-with-code`
     - `POST /api/auth/register-with-code`

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

### Migration: AddVerificationCodeToUser

**New Column:**

- **Table:** Users
- **Column:** VerificationCode
- **Type:** varchar/text (nullable)
- **Purpose:** Stores temporary verification codes

**Apply Migration:**

```bash
cd src/Services/Identity/Identity.Infrastructure
dotnet ef database update --startup-project ../Identity.API
```

## Security Considerations

### Best Practices Implemented

1. **Code Security:**

   - Cryptographically secure random generation
   - 5-digit codes (100,000 possible combinations)
   - Codes cleared after successful login

2. **Error Handling:**

   - Generic error messages to prevent enumeration
   - No distinction between invalid phone and invalid code

3. **Account Security:**
   - Status checks (disabled accounts cannot login)
   - Last login timestamp updated
   - Supports dual authentication (password + code)

### Recommendations for Production

1. **Rate Limiting:**

   - Limit verification code requests per phone (e.g., 3 per hour)
   - Limit login attempts per phone (e.g., 5 per 15 minutes)

2. **Code Expiration:**

   - Add timestamp to track code generation time
   - Expire codes after 10-15 minutes
   - Clean up old codes periodically

3. **SMS Delivery:**

   - Implement external OTP provider (Twilio, AWS SNS)
   - Log delivery status
   - Handle delivery failures gracefully

4. **Audit Logging:**
   - Log all verification code generations
   - Log successful/failed login attempts
   - Monitor for suspicious patterns

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

No additional configuration required. The feature uses existing JWT and database settings.

**Optional (for external provider):**

```json
{
  "OtpSettings": {
    "Provider": "Twilio",
    "AccountSid": "your-account-sid",
    "AuthToken": "your-auth-token",
    "FromNumber": "+1234567890"
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
