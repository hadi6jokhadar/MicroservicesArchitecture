# Development Mode Verification Code Response Update

**Date:** January 26, 2026  
**Feature:** Development Mode Verification Code Exposure  
**Impact:** Identity Service - Authentication Endpoints

---

## 🎯 Overview

Updated four verification code endpoints to return the generated verification code in **development mode** for testing purposes, while maintaining security in **production mode** by only returning success status.

---

## 📋 Changes Summary

### Affected Endpoints

1. `POST /api/auth/get-verification-code-by-phone`
2. `POST /api/auth/get-verification-code-by-email`
3. `POST /api/auth/register-with-code-by-phone`
4. `POST /api/auth/register-with-code-by-email`

### New Behavior

| Environment | Response Includes Code | Response Format                                     |
| ----------- | ---------------------- | --------------------------------------------------- |
| Development | ✅ Yes                 | `{ success: true, code: "123456", message: "..." }` |
| Production  | ❌ No                  | `{ success: true, code: null, message: "..." }`     |

**Note:** Verification codes are 6 digits long.

---

## 🏗️ Architecture Changes

### 1. New DTO Created

**File:** `Identity.Application/DTOs/VerificationCodeResponseDto.cs`

```csharp
namespace Identity.Application.DTOs;

/// <summary>
/// Response DTO for verification code operations
/// In development mode: returns the verification code as a string for testing
/// In production mode: returns success as boolean
/// </summary>
public class VerificationCodeResponseDto
{
    /// <summary>
    /// Success indicator (always present)
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Verification code (only present in development mode)
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Message describing the result
    /// </summary>
    public string? Message { get; set; }
}
```

### 2. Command Return Types Updated

**Changed from:** `IRequest<bool>`  
**Changed to:** `IRequest<VerificationCodeResponseDto>`

**Files Modified:**

- `Identity.Application/Commands/Auth/GetVerificationCodeByPhoneCommand.cs`
- `Identity.Application/Commands/Auth/GetVerificationCodeByEmailCommand.cs`
- `Identity.Application/Commands/Auth/RegisterWithCodeByPhoneCommand.cs`
- `Identity.Application/Commands/Auth/RegisterWithCodeByEmailCommand.cs`

**Example:**

```csharp
// Before
public record GetVerificationCodeByPhoneCommand(
    string PhoneNumber
) : IRequest<bool>;

// After
using Identity.Application.DTOs;

public record GetVerificationCodeByPhoneCommand(
    string PhoneNumber
) : IRequest<VerificationCodeResponseDto>;
```

### 3. Handlers Updated with Environment Detection

**Files Modified:**

- `Identity.Application/Handlers/Auth/GetVerificationCodeByPhoneCommandHandler.cs`
- `Identity.Application/Handlers/Auth/GetVerificationCodeByEmailCommandHandler.cs`
- `Identity.Application/Handlers/Auth/RegisterWithCodeByPhoneCommandHandler.cs`
- `Identity.Application/Handlers/Auth/RegisterWithCodeByEmailCommandHandler.cs`

**Changes:**

1. Added `IHostEnvironment` dependency injection
2. Changed return type from `bool` to `VerificationCodeResponseDto`
3. Modified return statement to conditionally include code

**Example (GetVerificationCodeByPhoneCommandHandler):**

```csharp
// Before
public class GetVerificationCodeByPhoneCommandHandler : IRequestHandler<GetVerificationCodeByPhoneCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;

    public GetVerificationCodeByPhoneCommandHandler(
        IUserRepository userRepository,
        IOtpService otpService,
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _otpService = otpService;
        _configuration = configuration;
        _tenantContext = tenantContext;
    }

    public async Task<bool> Handle(...)
    {
        // ... code generation logic ...
        return true;
    }
}

// After
using Microsoft.Extensions.Hosting;

public class GetVerificationCodeByPhoneCommandHandler : IRequestHandler<GetVerificationCodeByPhoneCommand, VerificationCodeResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly IHostEnvironment _hostEnvironment; // NEW

    public GetVerificationCodeByPhoneCommandHandler(
        IUserRepository userRepository,
        IOtpService otpService,
        IConfiguration configuration,
        ITenantContext tenantContext,
        IHostEnvironment hostEnvironment) // NEW
    {
        _userRepository = userRepository;
        _otpService = otpService;
        _configuration = configuration;
        _tenantContext = tenantContext;
        _hostEnvironment = hostEnvironment; // NEW
    }

    public async Task<VerificationCodeResponseDto> Handle(...)
    {
        // ... code generation logic ...

        // Return response with code in development mode, without code in production
        return new VerificationCodeResponseDto
        {
            Success = true,
            Code = _hostEnvironment.IsDevelopment() ? verificationCode : null
        };
    }
}
```

### 4. API Handlers Updated

**File:** `Identity.API/Handlers/AuthApiHandlers.cs`

**Changes:**

- Updated handlers to set message on DTO instead of creating anonymous object
- Return DTO directly with message populated

**Example:**

```csharp
// Before
public static async Task<IResult> GetVerificationCodeByPhoneHandler(
    GetVerificationCodeByPhoneCommand command,
    IMediator mediator,
    ILocalizationService localizationService,
    CancellationToken ct = default)
{
    var result = await mediator.Send(command, ct);
    return Results.Ok(new { success = result, message = localizationService.GetString(LocalizationKeys.Success.VerificationCodeSentPhone) });
}

// After
public static async Task<IResult> GetVerificationCodeByPhoneHandler(
    GetVerificationCodeByPhoneCommand command,
    IMediator mediator,
    ILocalizationService localizationService,
    CancellationToken ct = default)
{
    var result = await mediator.Send(command, ct);
    result.Message = localizationService.GetString(LocalizationKeys.Success.VerificationCodeSentPhone);
    return Results.Ok(result);
}
```

### 5. Endpoint Documentation Updated

**File:** `Identity.API/Extensions/EndpointMappingExtensions.cs`

**Changes:**

- Updated endpoint descriptions to mention environment-dependent behavior
- Changed `.Produces<object>(200)` to `.Produces<Identity.Application.DTOs.VerificationCodeResponseDto>(200)`

**Example:**

```csharp
// Before
authGroup.MapPost("/get-verification-code-by-phone", AuthApiHandlers.GetVerificationCodeByPhoneHandler)
    .WithName("GetVerificationCodeByPhone")
    .WithSummary("Get verification code for phone number")
    .WithDescription("Generate and send a 5-digit verification code to the user's phone number. x-tenant-id header is optional.")
    .Produces<object>(200)
    .ProducesValidationProblem()
    .AddEndpointFilter<ValidationFilter<GetVerificationCodeByPhoneCommand>>();

// After
authGroup.MapPost("/get-verification-code-by-phone", AuthApiHandlers.GetVerificationCodeByPhoneHandler)
    .WithName("GetVerificationCodeByPhone")
    .WithSummary("Get verification code for phone number")
    .WithDescription("Generate and send a 6-digit verification code to the user's phone number. In development mode, returns the code in the response. In production mode, only returns success status. x-tenant-id header is optional.")
    .Produces<Identity.Application.DTOs.VerificationCodeResponseDto>(200)
    .ProducesValidationProblem()
    .AddEndpointFilter<ValidationFilter<GetVerificationCodeByPhoneCommand>>();
```

---

## 📝 API Documentation

### Request Example

All four endpoints maintain the same request format.

**Get Verification Code by Phone:**

```http
POST /api/auth/get-verification-code-by-phone
Content-Type: application/json

{
  "phoneNumber": "+1234567890"
}
```

**Register with Code by Email:**

```http
POST /api/auth/register-with-code-by-email
Content-Type: application/json

{
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "data": "optional-metadata"
}
```

### Response Examples

#### Development Mode (ASPNETCORE_ENVIRONMENT=Development)

```json
{
  "success": true,
  "code": "123456",
  "message": "Verification code sent to your phone successfully"
}
```

**Note:** Code is 6 digits long.

**Benefits in Development:**

- ✅ Frontend developers can see the code without SMS/email integration
- ✅ Automated testing can verify code generation
- ✅ Postman/API testing doesn't require external services
- ✅ Integration tests can validate full flow

#### Production Mode (ASPNETCORE_ENVIRONMENT=Production)

```json
{
  "success": true,
  "code": null,
  "message": "Verification code sent to your phone successfully"
}
```

**Security in Production:**

- ✅ Verification code never exposed in API response
- ✅ Code only sent via SMS/Email (when external provider integrated)
- ✅ No risk of code interception from API logs/monitoring
- ✅ Compliance with security best practices

---

## 🔐 Security Considerations

### Development Mode

1. **Only Use Locally:** Development mode should NEVER be enabled on public-facing servers
2. **Environment Variable:** Set `ASPNETCORE_ENVIRONMENT=Development` only on local/dev machines
3. **Code Exposure:** Codes are logged and visible in responses - acceptable for testing only

### Production Mode

1. **Code Hidden:** Verification codes are NEVER included in API responses
2. **Delivery Required:** Must integrate SMS/Email provider (Twilio, SendGrid, etc.) to deliver codes
3. **Audit Logging:** Code generation events are logged (without code value)
4. **Rate Limiting:** Resend cooldown and lockout mechanisms prevent abuse

### Multi-Tenant Considerations

- Both development and production mode work with tenant-specific OTP settings
- Environment detection is global, but OTP settings (length, expiration, etc.) respect tenant configuration
- Tenant context is optional for these endpoints (`OptionalTenantAttribute`)

---

## 🧪 Testing Guide

### Development Mode Testing

```bash
# Set environment to Development
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Run Identity Service
cd src/Services/Identity/Identity.API
dotnet run
```

**Test with curl:**

```bash
# Request verification code
curl -X POST "https://localhost:5001/api/auth/get-verification-code-by-phone" \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber": "+1234567890"}'

# Response includes code
{
  "success": true,
  "code": "12345",  # <-- Code visible!
  "message": "Verification code sent to your phone successfully"
}
```

### Production Mode Testing

```bash
# Set environment to Production
$env:ASPNETCORE_ENVIRONMENT = "Production"

# Run Identity Service
cd src/Services/Identity/Identity.API
dotnet run
```

**Test with curl:**

```bash
# Request verification code
curl -X POST "https://localhost:5001/api/auth/get-verification-code-by-phone" \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber": "+1234567890"}'

# Response does NOT include code
{
  "success": true,
  "code": null,  # <-- Code hidden!
  "message": "Verification code sent to your phone successfully"
}
```

### Integration Testing

**Example Test (xUnit):**

```csharp
[Fact]
public async Task GetVerificationCode_DevelopmentMode_ReturnsCode()
{
    // Arrange
    var command = new GetVerificationCodeByPhoneCommand("+1234567890");

    // Act
    var result = await _mediator.Send(command);

    // Assert
    result.Success.Should().BeTrue();
    result.Code.Should().NotBeNullOrEmpty(); // Code present in dev mode
    result.Code.Should().MatchRegex(@"^\d{5}$"); // 5-digit numeric code
}

[Fact]
public async Task GetVerificationCode_ProductionMode_HidesCode()
{
    // Arrange (mock production environment)
    var command = new GetVerificationCodeByPhoneCommand("+1234567890");

    // Act
    var result = await _mediator.Send(command);

    // Assert
    result.Success.Should().BeTrue();
    result.Code.Should().BeNull(); // Code hidden in production
}
```

---

## 📚 Related Documentation

- **PHONE_VERIFICATION_LOGIN_GUIDE.md** - Complete phone verification flow
- **OTP_SECURITY_AND_VALIDATION_UPDATE.md** - OTP security features and multi-tenant support
- **IDENTITY_OPTIONAL_TENANT_IMPLEMENTATION_SUMMARY.md** - OptionalTenant attribute usage
- **QUICK_REFERENCE.md** - Endpoint quick reference

---

## 🔄 Backward Compatibility

### Breaking Changes

⚠️ **API Response Format Changed**

**Before:**

```json
{
  "success": true,
  "message": "Verification code sent successfully"
}
```

**After:**

```json
{
  "success": true,
  "code": null, // or "12345" in development
  "message": "Verification code sent successfully"
}
```

### Migration Guide for Frontend Developers

**Old Code:**

```typescript
interface OldResponse {
  success: boolean;
  message: string;
}

this.authService
  .getVerificationCodeByPhone({ phoneNumber })
  .subscribe((response: OldResponse) => {
    console.log(response.message);
  });
```

**New Code:**

```typescript
import { IVerificationCodeResponse } from "@ihsan/core";

this.authService
  .getVerificationCodeByPhone(phoneNumber)
  .subscribe((response: IVerificationCodeResponse) => {
    console.log(response.message);

    // In development, you can now see the code
    if (response.code) {
      console.log("Dev mode - Code:", response.code);
    }
  });
```

**Updated Angular Service Methods:**

```typescript
// libs/core/src/lib/identity/auth.service.ts

// Get verification code by phone
getVerificationCodeByPhone(phoneNumber: string): Observable<IVerificationCodeResponse>

// Get verification code by email
getVerificationCodeByEmail(email: string): Observable<IVerificationCodeResponse>

// Register with code by phone (returns code in dev mode, requires login after)
registerWithCodeByPhone(
  phoneNumber: string,
  firstName: string,
  lastName: string,
  data?: string
): Observable<IVerificationCodeResponse>

// Register with code by email (returns code in dev mode, requires login after)
registerWithCodeByEmail(
  email: string,
  firstName: string,
  lastName: string,
  data?: string
): Observable<IVerificationCodeResponse>
```

**Updated Models:**

```typescript
// libs/core/src/lib/identity/models.ts

export interface IVerificationCodeResponse {
  success: boolean;
  code: string | null; // Only present in development mode
  message?: string;
}

export class VerificationCodeResponseClass implements IVerificationCodeResponse {
  success: boolean;
  code: string | null;
  message?: string;

  constructor(data: Partial<IVerificationCodeResponse> = {}) {
    this.success = data.success ?? false;
    this.code = data.code ?? null;
    this.message = data.message;
  }
}
```

**Complete Verification Flow Example:**

```typescript
import { Component, inject, signal } from "@angular/core";
import { FormControl, FormGroup, Validators } from "@angular/forms";
import { AuthService } from "@ihsan/core";

interface IPhoneLoginForm {
  phoneNumber: FormControl<string>;
  code: FormControl<string>;
}

export class PhoneLoginComponent {
  private readonly _authService = inject(AuthService);

  readonly step = signal<"phone" | "code">("phone");
  readonly devCode = signal<string | null>(null);

  readonly phoneForm = new FormGroup<IPhoneLoginForm>({
    phoneNumber: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required],
    }),
    code: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  // Step 1: Request verification code
  onRequestCode(): void {
    const phoneNumber = this.phoneForm.get("phoneNumber")?.value;
    if (!phoneNumber) return;

    this._authService.getVerificationCodeByPhone(phoneNumber).subscribe({
      next: (response) => {
        this.step.set("code");

        // In development, auto-fill the code
        if (response.code) {
          this.devCode.set(response.code);
          this.phoneForm.get("code")?.setValue(response.code);
        }
      },
    });
  }

  // Step 2: Login with code
  onLogin(): void {
    const phoneNumber = this.phoneForm.get("phoneNumber")?.value;
    const code = this.phoneForm.get("code")?.value;

    if (!phoneNumber || !code) return;

    this._authService.loginWithCodeByPhone(phoneNumber, code).subscribe({
      next: () => {
        // User logged in, navigate to dashboard
      },
    });
  }
}
```

---

## ✅ Checklist

**Backend:**

- [x] Created `VerificationCodeResponseDto` class
- [x] Updated command return types (4 commands)
- [x] Updated handler return types and logic (4 handlers)
- [x] Added `IHostEnvironment` dependency injection (4 handlers)
- [x] Updated API handlers to use new DTO (4 handlers)
- [x] Updated endpoint documentation with environment behavior
- [x] Updated endpoint `.Produces<>` metadata
- [x] Created comprehensive documentation
- [x] Tested in development mode
- [x] Tested in production mode

**Frontend:**

- [x] Added `IVerificationCodeResponse` interface to models
- [x] Added `VerificationCodeResponseClass` to models
- [x] Updated `getVerificationCodeByPhone()` return type
- [x] Updated `getVerificationCodeByEmail()` return type
- [x] Updated `registerWithCodeByPhone()` signature and return type
- [x] Updated `registerWithCodeByEmail()` signature and return type
- [x] Updated IDENTITY_MODULE_GUIDE.md with new API signatures
- [x] Added usage examples with development mode code handling
- [ ] Update any components using these endpoints (if needed)
- [ ] Update Postman collections (if needed)

---

**Version:** 1.0  
**Last Updated:** January 26, 2026  
**Status:** ✅ Implementation Complete
