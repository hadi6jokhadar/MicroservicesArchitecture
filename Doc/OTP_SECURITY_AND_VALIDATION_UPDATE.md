# OTP Security & Validation System - Complete Implementation

**Date:** October 30, 2025  
**Feature:** Enhanced OTP Security with Multi-Tenant Support and Dynamic Validation

---

## 🎯 Overview

This document describes the complete OTP (One-Time Password) security system with configurable settings, multi-tenant support, and dynamic validation.

---

## 📋 Features Implemented

### 1. **OTP Security Features**

- ✅ Configurable code length (4-10 characters)
- ✅ Configurable code expiration (30-1800 seconds)
- ✅ Maximum failed attempts limit (1-10 attempts)
- ✅ Automatic account lockout after max attempts
- ✅ Configurable lockout duration (1-60 minutes)
- ✅ Resend cooldown period (10-300 seconds)
- ✅ Alphanumeric or numeric code support
- ✅ Optional secret key for enhanced security

### 2. **Multi-Tenant Support**

- ✅ Tenant-specific OTP configuration
- ✅ Per-tenant security policies (enterprise, standard, internal)
- ✅ Graceful fallback to appsettings.json
- ✅ Dynamic configuration resolution at runtime

### 3. **Dynamic Validation**

- ✅ FluentValidation with tenant-aware rules
- ✅ Validation adapts to resolved OTP settings
- ✅ No hardcoded validation rules
- ✅ Startup configuration validation

---

## 🏗️ Architecture

### Configuration Resolution Flow

```
┌─────────────────────────────────────────────────────────────┐
│                    Request Arrives                           │
│              (with or without x-tenant-id)                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────────┐
│            TenantMiddleware (if enabled)                     │
│    - Resolves tenant from header/domain/claim               │
│    - Populates ITenantContext                                │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────────┐
│               Validator Executes                             │
│  - LoginWithCodeByPhoneCommandValidator                      │
│  - LoginWithCodeByEmailCommandValidator                      │
│                                                              │
│  GetOtpSettings(configuration, tenantContext)                │
│    ├─ Check: tenantContext.IsMultiTenantMode?               │
│    ├─ Check: tenantContext.HasTenant?                       │
│    ├─ Check: tenantContext.CurrentTenant?.Configuration?.Otp?│
│    │   ├─ YES → Return Tenant OTP Settings                  │
│    │   └─ NO  → Return appsettings.json OTP Settings        │
│    └─ Validate code length & format dynamically             │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────────┐
│              Handler Executes                                │
│  - GetVerificationCodeByPhoneCommandHandler                  │
│  - LoginWithCodeByPhoneCommandHandler                        │
│  - RegisterWithCodeByPhoneCommandHandler                     │
│  (+ Email variants)                                          │
│                                                              │
│  GetOtpSettings()                                            │
│    ├─ Same resolution logic as validator                    │
│    └─ Use settings for:                                     │
│        • Code generation (length, alphanumeric)             │
│        • Expiration time calculation                        │
│        • Failed attempts checking                           │
│        • Lockout duration                                   │
│        • Resend cooldown                                    │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────────┐
│                Response to Client                            │
│  - Token (success) or Error with remaining attempts          │
└─────────────────────────────────────────────────────────────┘
```

### Configuration Priority

```
Priority 1: Tenant.Configuration.Otp
     ↓ (if multi-tenancy enabled AND tenant has OTP config)
Priority 2: appsettings.json OtpSettings
     ↓ (fallback)
Priority 3: new OtpSettings() with hardcoded defaults
```

---

## 📁 Files Modified

### 1. **Shared Kernel** (`IhsanDev.Shared.Kernel`)

**File:** `Dto/Tenant/TenantInfo.cs`

- ✅ Added `OtpSettings` class to `TenantConfiguration`
- ✅ Removed Data Annotations (validation happens after resolution)
- ✅ Properties: CodeLength, ExpirationSeconds, MaxAttempts, LockoutMinutes, ResendCooldownSeconds, UseAlphanumeric, SecretKey

### 2. **Shared Infrastructure** (`IhsanDev.Shared.Infrastructure`)

**File:** `Extensions/ConfigurationValidationExtensions.cs` (NEW)

- ✅ Extension method: `ValidateOtpSettings(IConfiguration, ILogger?)`
- ✅ Validates resolved OTP settings at application startup
- ✅ Throws exception if configuration is invalid
- ✅ Prevents app from starting with bad configuration

**File:** `Middleware/DatabaseMigrationMiddleware.cs`

- ✅ Added `ShouldSkipMigration()` method
- ✅ Skips migration for: `/swagger*`, `/health*`, `/metrics*`
- ✅ Prevents migration logs on static resource requests

**File:** `Middleware/DefaultDatabaseMigrationMiddleware.cs`

- ✅ Added `ShouldSkipMigration()` method
- ✅ Same skip logic as tenant migration middleware

### 3. **Identity Application** (`Identity.Application`)

**Commands:**

- `Commands/Auth/LoginWithCodeByPhoneCommand.cs`

  - ✅ Validator injects: `IConfiguration`, `ITenantContext`
  - ✅ Dynamic validation based on resolved OTP settings
  - ✅ `GetOtpSettings()` method for tenant-aware resolution

- `Commands/Auth/LoginWithCodeByEmailCommand.cs`
  - ✅ Same pattern as phone validator
  - ✅ Tenant-aware validation

**Handlers:** (All 6 handlers updated)

- `GetVerificationCodeByPhoneCommandHandler.cs`
- `GetVerificationCodeByEmailCommandHandler.cs`
- `LoginWithCodeByPhoneCommandHandler.cs`
- `LoginWithCodeByEmailCommandHandler.cs`
- `RegisterWithCodeByPhoneCommandHandler.cs`
- `RegisterWithCodeByEmailCommandHandler.cs`

**All handlers include:**

- ✅ Inject: `IConfiguration`, `ITenantContext`, `IOtpService`, `IUserRepository`
- ✅ `GetOtpSettings()` private method
- ✅ Tenant-aware OTP configuration resolution
- ✅ Security logic: expiration, attempts, lockout, cooldown
- ✅ Fallback to appsettings.json

### 4. **Identity Infrastructure** (`Identity.Infrastructure`)

**File:** `Helpers/ConfigurationHelper.cs`

- ✅ Static method: `GetOtpSettings(IConfiguration, ITenantContext)`
- ✅ Centralized OTP configuration resolution
- ✅ Consistent with JWT and Database configuration pattern

### 5. **Identity API** (`Identity.API`)

**File:** `Program.cs`

- ✅ Added: `builder.Configuration.ValidateOtpSettings(logger);`
- ✅ Validates OTP settings at startup (before app runs)
- ✅ Logs validation success/failure
- ✅ Removed Swagger environment restriction (enabled for all environments)

---

## ⚙️ Configuration

### Single-Tenant Mode (appsettings.json)

```json
{
  "MultiTenancy": {
    "Enabled": false
  },
  "OtpSettings": {
    "CodeLength": 6,
    "ExpirationSeconds": 300,
    "MaxAttempts": 3,
    "LockoutMinutes": 15,
    "ResendCooldownSeconds": 60,
    "UseAlphanumeric": false,
    "SecretKey": "your-secret-key-here-min-32-chars-for-production-use"
  }
}
```

### Multi-Tenant Mode

**Identity Service (appsettings.json):**

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "http://localhost:5002"
  },
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

**Tenant Configuration (Tenant Service):**

```json
{
  "tenantId": "enterprise-corp",
  "tenantName": "Enterprise Corporation",
  "isActive": true,
  "configuration": {
    "database": { "connectionString": "..." },
    "jwt": { "secret": "...", "issuer": "...", "audience": "..." },
    "otp": {
      "codeLength": 8,
      "expirationSeconds": 600,
      "maxAttempts": 5,
      "lockoutMinutes": 30,
      "resendCooldownSeconds": 120,
      "useAlphanumeric": true,
      "secretKey": "enterprise-secure-key-32-chars-min"
    }
  }
}
```

---

## 🔒 Validation Rules

### Startup Validation (ConfigurationValidationExtensions)

Validates appsettings.json OTP configuration at application startup:

| Property              | Valid Range                     | Default |
| --------------------- | ------------------------------- | ------- |
| CodeLength            | 4-10                            | 6       |
| ExpirationSeconds     | 30-1800 (30 sec - 30 min)       | 300     |
| MaxAttempts           | 1-10                            | 3       |
| LockoutMinutes        | 1-60                            | 15      |
| ResendCooldownSeconds | 10-300 (10 sec - 5 min)         | 60      |
| SecretKey             | Min 32 characters (if provided) | null    |

**Behavior:**

- ✅ Logs validation errors
- ✅ Throws `InvalidOperationException` if invalid
- ✅ Prevents application startup with bad configuration

### Runtime Validation (FluentValidation)

Validates verification code based on resolved OTP settings:

| Property         | Validation                                                              |
| ---------------- | ----------------------------------------------------------------------- |
| PhoneNumber      | Required, E.164 format (`^\+?[1-9]\d{1,14}$`)                           |
| Email            | Required, Valid email address                                           |
| VerificationCode | Required, Exact length (dynamic), Digits only or alphanumeric (dynamic) |

**Example Error Messages:**

- `"Verification code must be 6 characters"` (if CodeLength = 6)
- `"Verification code must be 8 characters"` (if CodeLength = 8)
- `"Verification code must contain only digits"` (if UseAlphanumeric = false)
- `"Verification code must contain only letters and digits"` (if UseAlphanumeric = true)

---

## 🔐 Security Features

### 1. **Code Expiration**

```csharp
// Handler generates code with expiration
var otpSettings = GetOtpSettings();
var expiryTime = DateTime.UtcNow.AddSeconds(otpSettings.ExpirationSeconds);
user.VerificationCodeExpiry = expiryTime;

// Handler validates expiration on login
if (!user.VerificationCodeExpiry.HasValue ||
    user.VerificationCodeExpiry.Value < DateTime.UtcNow)
{
    throw new UnauthorizedException("Verification code has expired. Please request a new code.");
}
```

### 2. **Failed Attempts & Lockout**

```csharp
// Increment failed attempts
user.FailedCodeAttempts += 1;

// Check if max attempts reached
if (user.FailedCodeAttempts >= otpSettings.MaxAttempts)
{
    user.CodeLockoutUntil = DateTime.UtcNow.AddMinutes(otpSettings.LockoutMinutes);
    await _userRepository.UpdateAsync(user, cancellationToken);
    throw new ForbiddenException($"Too many failed attempts. Account is locked for {otpSettings.LockoutMinutes} minute(s).");
}

// Show remaining attempts
var remainingAttempts = otpSettings.MaxAttempts - user.FailedCodeAttempts;
throw new UnauthorizedException($"Email or verification code is incorrect. {remainingAttempts} attempt(s) remaining.");
```

### 3. **Resend Cooldown**

```csharp
// Check if cooldown period has passed
if (user.LastCodeSentAt.HasValue)
{
    var timeSinceLastCode = DateTime.UtcNow - user.LastCodeSentAt.Value;
    if (timeSinceLastCode.TotalSeconds < otpSettings.ResendCooldownSeconds)
    {
        var remainingSeconds = (int)(otpSettings.ResendCooldownSeconds - timeSinceLastCode.TotalSeconds);
        throw new ForbiddenException($"Please wait {remainingSeconds} second(s) before requesting another code.");
    }
}
```

### 4. **Account Lockout Check**

```csharp
// Check if user is locked out
if (user.CodeLockoutUntil.HasValue && user.CodeLockoutUntil.Value > DateTime.UtcNow)
{
    var remainingMinutes = (int)(user.CodeLockoutUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
    throw new ForbiddenException($"Account is temporarily locked due to too many failed attempts. Please try again in {remainingMinutes} minute(s).");
}
```

### 5. **Successful Login - Reset Security Fields**

```csharp
// Clear verification code and reset security fields after successful login
user.VerificationCode = null;
user.VerificationCodeExpiry = null;
user.FailedCodeAttempts = 0;
user.CodeLockoutUntil = null;
user.LastLogin = DateTime.UtcNow;
user.LastModified = DateTime.UtcNow;
await _userRepository.UpdateAsync(user, cancellationToken);
```

---

## 🧪 Testing Scenarios

### Scenario 1: Valid Code (Success)

```
Request: POST /api/auth/login-with-code-by-phone
Body: { "phoneNumber": "+1234567890", "verificationCode": "123456" }

Expected: 200 OK with JWT tokens
```

### Scenario 2: Wrong Code (1st Attempt)

```
Request: POST /api/auth/login-with-code-by-phone
Body: { "phoneNumber": "+1234567890", "verificationCode": "999999" }

Expected: 401 Unauthorized
Message: "Phone number or verification code is incorrect. 2 attempt(s) remaining."
```

### Scenario 3: Max Attempts Reached

```
Request: POST /api/auth/login-with-code-by-phone (after 3 failed attempts)
Body: { "phoneNumber": "+1234567890", "verificationCode": "999999" }

Expected: 403 Forbidden
Message: "Too many failed attempts. Account is locked for 15 minute(s)."
```

### Scenario 4: Expired Code

```
Request: POST /api/auth/login-with-code-by-phone (after 5 minutes)
Body: { "phoneNumber": "+1234567890", "verificationCode": "123456" }

Expected: 401 Unauthorized
Message: "Verification code has expired. Please request a new code."
```

### Scenario 5: Resend Too Soon

```
Request: POST /api/auth/get-verification-code-by-phone (within 60 seconds)
Body: { "phoneNumber": "+1234567890" }

Expected: 403 Forbidden
Message: "Please wait 45 second(s) before requesting another code."
```

### Scenario 6: Invalid Code Length (Validation)

```
Request: POST /api/auth/login-with-code-by-phone
Body: { "phoneNumber": "+1234567890", "verificationCode": "12345" }

Expected: 400 Bad Request
Message: "Verification code must be 6 characters"
```

### Scenario 7: Tenant-Specific Settings

```
Request: POST /api/auth/login-with-code-by-phone
Headers: { "x-tenant-id": "enterprise-corp" }
Body: { "phoneNumber": "+1234567890", "verificationCode": "12345678" }

Expected: Validates against tenant's 8-digit alphanumeric code settings
```

---

## 📊 Use Cases

### Enterprise Tenant (Stricter Security)

```json
{
  "otp": {
    "codeLength": 8,
    "expirationSeconds": 600,
    "maxAttempts": 2,
    "lockoutMinutes": 30,
    "resendCooldownSeconds": 120,
    "useAlphanumeric": true
  }
}
```

- 8-character alphanumeric codes
- 10-minute expiration
- Only 2 attempts before 30-minute lockout
- 2-minute resend cooldown

### Standard Tenant (Balanced)

```json
{
  "otp": {
    "codeLength": 6,
    "expirationSeconds": 300,
    "maxAttempts": 3,
    "lockoutMinutes": 15,
    "resendCooldownSeconds": 60,
    "useAlphanumeric": false
  }
}
```

- 6-digit numeric codes
- 5-minute expiration
- 3 attempts before 15-minute lockout
- 1-minute resend cooldown

### Development/Internal Tenant (Lenient)

```json
{
  "otp": {
    "codeLength": 4,
    "expirationSeconds": 1800,
    "maxAttempts": 10,
    "lockoutMinutes": 1,
    "resendCooldownSeconds": 10,
    "useAlphanumeric": false
  }
}
```

- 4-digit numeric codes
- 30-minute expiration
- 10 attempts before 1-minute lockout
- 10-second resend cooldown

---

## 🔄 Migration Path

### Database Migration

Run the existing migration that added OTP security fields:

```bash
dotnet ef migrations add UpdateOtpSecurityFields --project Identity.Infrastructure --startup-project Identity.API
dotnet ef database update --project Identity.Infrastructure --startup-project Identity.API
```

**Fields Added:**

- `VerificationCodeExpiry` (DateTime?)
- `FailedCodeAttempts` (int)
- `CodeLockoutUntil` (DateTime?)
- `LastCodeSentAt` (DateTime?)

---

## ✅ Validation Checklist

### Pre-Deployment

- [x] All 6 OTP handlers updated with security logic
- [x] Both validators updated with dynamic validation
- [x] ConfigurationValidationExtensions created
- [x] Startup validation added to Program.cs
- [x] Database migration created and applied
- [x] Multi-tenant OTP configuration tested
- [x] Fallback to appsettings.json tested
- [x] Data Annotations removed (validation after resolution)
- [x] Build successful (0 errors)

### Post-Deployment Testing

- [ ] Test single-tenant mode (appsettings.json only)
- [ ] Test multi-tenant mode (tenant-specific settings)
- [ ] Test fallback when tenant has no OTP config
- [ ] Test code expiration
- [ ] Test failed attempts and lockout
- [ ] Test resend cooldown
- [ ] Test invalid code length (validation)
- [ ] Test alphanumeric vs numeric codes
- [ ] Test startup validation with invalid config

---

## 📚 Related Documentation

- [Phone Verification Login Guide](./PHONE_VERIFICATION_LOGIN_GUIDE.md)
- [Phone Verification Implementation Summary](./PHONE_VERIFICATION_IMPLEMENTATION_SUMMARY.md)
- [Phone Verification Quick Reference](./PHONE_VERIFICATION_QUICK_REFERENCE.md)
- [Multi-Tenancy Guide](./MULTI_TENANCY_GUIDE.md)
- [Quick Reference](./QUICK_REFERENCE.md)

---

## 🎉 Summary

### Key Achievements

1. **Security**: Comprehensive OTP security with configurable policies
2. **Flexibility**: Per-tenant OTP configuration for different security requirements
3. **Validation**: Dynamic validation that adapts to resolved configuration
4. **Clean Architecture**: No Data Annotations on DTOs, validation happens at the right time
5. **Fail-Safe**: Startup validation prevents app from running with bad configuration
6. **Backward Compatible**: Graceful fallback to appsettings.json for single-tenant or tenants without custom OTP settings

### Configuration Flow Summary

```
Startup:
  └─ ValidateOtpSettings(appsettings.json) → Throws if invalid

Request (Multi-Tenant):
  ├─ Validator: GetOtpSettings(tenant or appsettings) → Validate code dynamically
  └─ Handler: GetOtpSettings(tenant or appsettings) → Apply security logic

Request (Single-Tenant):
  ├─ Validator: GetOtpSettings(appsettings) → Validate code dynamically
  └─ Handler: GetOtpSettings(appsettings) → Apply security logic
```

---

**Implementation Complete** ✅  
**Documentation Updated** ✅  
**Ready for Production** ✅
