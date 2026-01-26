# Phone Verification Login - Quick Reference

**Last Updated:** January 26, 2026  
**See Also:** [VERIFICATION_CODE_DEVELOPMENT_MODE_UPDATE.md](VERIFICATION_CODE_DEVELOPMENT_MODE_UPDATE.md) for development mode behavior

## 🚀 Quick Start

### Testing the Feature (Development)

1. **Start the Identity Service:**

```bash
cd src/Services/Identity/Identity.API
dotnet run
```

2. **Access Swagger UI:**

```
https://localhost:5101/swagger
```

3. **Apply Database Migration:**

```bash
cd src/Services/Identity/Identity.Infrastructure
$env:MultiTenancy__Enabled="false"
dotnet ef database update --startup-project ../Identity.API
```

## 📋 API Endpoints

### 1️⃣ Get Verification Code

```http
POST /api/auth/get-verification-code
Content-Type: application/json

{
  "phoneNumber": "+1234567890"
}
```

**Response (Production):**

```json
{
  "success": true,
  "code": null,
  "message": "Verification code sent successfully"
}
```

**Response (Development):**

```json
{
  "success": true,
  "code": "12345",
  "message": "Verification code sent successfully"
}
```

> **Note:** In development mode (`ASPNETCORE_ENVIRONMENT=Development`), the verification code is included in the response for testing. In production, `code` is always `null` for security. See [VERIFICATION_CODE_DEVELOPMENT_MODE_UPDATE.md](VERIFICATION_CODE_DEVELOPMENT_MODE_UPDATE.md).

### 2️⃣ Login with Code

```http
POST /api/auth/login-with-code
Content-Type: application/json

{
  "phoneNumber": "+1234567890",
  "verificationCode": "12345"
}
```

**Response:**

```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "abc123...",
  "user": { ... }
}
```

### 3️⃣ Register with Code

```http
POST /api/auth/register-with-code
Content-Type: application/json

{
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890"
}
```

**Response (Production):**

```json
{
  "success": true,
  "code": null,
  "message": "Registration successful. Please login with the verification code sent to your phone."
}
```

**Response (Development):**

```json
{
  "success": true,
  "code": "12345",
  "message": "Registration successful. Please login with the verification code sent to your phone."
}
```

> **Note:** In development mode, the verification code is included in the response. See [VERIFICATION_CODE_DEVELOPMENT_MODE_UPDATE.md](VERIFICATION_CODE_DEVELOPMENT_MODE_UPDATE.md).

## 🧪 Testing Flow

### New User Registration:

```
1. POST /api/auth/register-with-code
2. Check database for verification code
3. POST /api/auth/login-with-code (with code from DB)
4. Receive JWT tokens
```

### Existing User Login:

```
1. POST /api/auth/get-verification-code
2. Check database for verification code
3. POST /api/auth/login-with-code (with code from DB)
4. Receive JWT tokens
```

## 🔍 Check Verification Code (Development)

**PostgreSQL:**

```sql
SELECT Id, PhoneNumber, VerificationCode, Email, FirstName, LastName
FROM "Users"
WHERE "PhoneNumber" = '+1234567890';
```

**SQL Server:**

```sql
SELECT Id, PhoneNumber, VerificationCode, Email, FirstName, LastName
FROM Users
WHERE PhoneNumber = '+1234567890';
```

## ✅ Validation Rules

| Field             | Rules                                         |
| ----------------- | --------------------------------------------- |
| Phone Number      | Required, E.164 format (`^\+?[1-9]\d{1,14}$`) |
| Verification Code | 5 digits, numeric only (`^\d{5}$`)            |
| Email             | Valid email, max 256 chars                    |
| First/Last Name   | Letters only, max 100 chars                   |

## 🔐 Security Features

- ✅ **Code Expiration:** Codes expire after 5 minutes (configurable)
- ✅ **Failed Attempt Tracking:** Max 3 failed attempts before lockout
- ✅ **Account Lockout:** 15-minute lockout after max attempts exceeded
- ✅ **Resend Cooldown:** 60-second minimum between code requests
- ✅ **Cryptographically Secure:** RandomNumberGenerator for code generation
- ✅ **Configurable Codes:** Numeric or alphanumeric, 4-10 characters
- ✅ **Generic Error Messages:** Prevents phone/email enumeration
- ✅ **Account Status Validation:** Disabled accounts cannot authenticate
- ✅ **Multi-Tenant Support:** Per-tenant OTP configuration
- ✅ **Code Clearing:** Codes cleared after successful login

## 📂 Key Files

| Layer           | File                                                   | Purpose                |
| --------------- | ------------------------------------------------------ | ---------------------- |
| **Shared**      | `IhsanDev.Shared.Infrastructure/Services/Otp/`         | OTP service            |
| **Domain**      | `Identity.Domain/Entities/User.cs`                     | Added VerificationCode |
| **Application** | `Identity.Application/Commands/Auth/`                  | 3 new commands         |
| **Application** | `Identity.Application/Handlers/Auth/`                  | 3 new handlers         |
| **API**         | `Identity.API/Extensions/EndpointMappingExtensions.cs` | Endpoint mapping       |

## 🛠️ Configuration

### **Required Configuration (appsettings.json)**

Add `OtpSettings` section to Identity Service:

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

| Setting                 | Default | Description                            |
| ----------------------- | ------- | -------------------------------------- |
| `CodeLength`            | 6       | Length of OTP code                     |
| `ExpirationSeconds`     | 300     | Code validity (5 minutes)              |
| `MaxAttempts`           | 3       | Failed attempts before lockout         |
| `LockoutMinutes`        | 15      | Lockout duration                       |
| `ResendCooldownSeconds` | 60      | Cooldown between code requests         |
| `UseAlphanumeric`       | false   | Alphanumeric (true) or numeric (false) |

### **Multi-Tenant Configuration (Optional)**

Enable per-tenant OTP settings by setting `MultiTenancy:Enabled = true`:

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "https://localhost:5002"
  }
}
```

Tenants can have custom OTP settings in their tenant configuration (stored in Tenant Service). If tenant has no custom settings, falls back to appsettings.json.

**Example Tenant OTP Configuration:**

```json
{
  "tenantId": "acme",
  "configuration": {
    "otp": {
      "codeLength": 8,
      "expirationSeconds": 600,
      "maxAttempts": 5,
      "lockoutMinutes": 30,
      "resendCooldownSeconds": 120,
      "useAlphanumeric": true
    }
  }
}
```

### **External SMS Provider (Optional)**

```csharp
// Implement IExternalOtpProvider for Twilio, AWS SNS, etc.
services.AddScoped<IExternalOtpProvider, TwilioOtpProvider>();
```

## 🐛 Troubleshooting

| Error                                                  | Cause                                | Solution                                                                     |
| ------------------------------------------------------ | ------------------------------------ | ---------------------------------------------------------------------------- |
| "Phone number or verification code is incorrect"       | Invalid code or code expired         | Request new code via /get-verification-code                                  |
| "Your account has been locked"                         | Too many failed attempts             | Wait for lockout duration (default: 15 minutes)                              |
| "Please wait before requesting another code"           | Resend cooldown active               | Wait for cooldown period (default: 60 seconds)                               |
| "Account is disabled"                                  | User status is false                 | Admin must enable account                                                    |
| "Phone number not found"                               | User not registered                  | Use POST /register-with-code-by-phone first                                  |
| "Verification code has expired"                        | Code older than expiration time      | Request new code (codes expire after 5 minutes default)                      |
| Migration fails with multi-tenancy error               | Multi-tenancy check during migration | Set `$env:MultiTenancy__Enabled="false"` before running migration            |
| OTP settings not working                               | Missing OtpSettings section          | Add OtpSettings section to appsettings.json                                  |
| Tenant-specific OTP settings ignored                   | Multi-tenancy not enabled            | Set `MultiTenancy:Enabled = true` in appsettings.json                        |
| Different tenants getting same OTP expiration/cooldown | Tenant has no custom OTP settings    | Add "otp" section to tenant configuration in Tenant Service                  |
| Codes not expiring or lockout not working              | Database migration not applied       | Run `dotnet ef database update` to apply UpdateOtpSecurityFields migration   |
| FailedCodeAttempts always 0                            | Migration missing                    | Ensure UpdateOtpSecurityFields migration (20251030154411) has been applied   |
| User can request unlimited codes without cooldown      | LastCodeSentAt field missing         | Verify UpdateOtpSecurityFields migration applied and database schema updated |

## 📖 Full Documentation

- **Feature Guide:** `Doc/PHONE_VERIFICATION_LOGIN_GUIDE.md`
- **Implementation Summary:** `Doc/PHONE_VERIFICATION_IMPLEMENTATION_SUMMARY.md`
- **API Docs:** Swagger UI at `/swagger`

## 🎯 Curl Examples

### Register:

```bash
curl -X POST "https://localhost:5101/api/auth/register-with-code" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "firstName": "Test",
    "lastName": "User",
    "phoneNumber": "+1234567890"
  }'
```

### Login:

```bash
curl -X POST "https://localhost:5101/api/auth/login-with-code" \
  -H "Content-Type: application/json" \
  -d '{
    "phoneNumber": "+1234567890",
    "verificationCode": "12345"
  }'
```

## 🚦 Status

- ✅ **Implementation:** Complete with Security Enhancements
- ✅ **Build:** No Errors (verified with `dotnet build`)
- ✅ **Database:** 2 Migrations Created (AddVerificationCodeToUser + UpdateOtpSecurityFields)
- ✅ **Security Features:** Code expiration, failed attempts, lockout, cooldown
- ✅ **Multi-Tenancy:** Per-tenant OTP configuration support
- ✅ **Configuration:** Global (appsettings) + Per-Tenant options
- ✅ **Documentation:** Complete and up-to-date
- ✅ **Ready for:** Production Deployment
- ⏳ **SMS Integration:** Optional Enhancement (Twilio/AWS SNS)

---

**Last Updated:** October 30, 2025  
**Version:** 2.0 (Security & Multi-Tenancy Update)  
**Migration:** UpdateOtpSecurityFields (20251030154411)
