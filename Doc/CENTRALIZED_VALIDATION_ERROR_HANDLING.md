# Centralized Validation Error Handling - Implementation Summary

> **Audit correction (August 2026):** this doc only ever described the FluentValidation/endpoint-filter
> half of the pipeline. The other half — what actually turns *any* exception (including a
> FluentValidation `ValidationException` thrown from the MediatR `ValidationBehavior` pipeline
> behavior, not just one caught by the endpoint filter below) into the JSON response — is
> `GlobalExceptionHandler` (`IhsanDev.Shared.Infrastructure/Middleware/GlobalExceptionHandler.cs`),
> an ASP.NET Core `IExceptionHandler` registered via `AddGlobalExceptionHandler()`/
> `app.UseGlobalExceptionHandler()`. **Do not confuse it with `GlobalExceptionHandlingMiddleware`**
> (same folder) — that class is dead code with a different, older response shape; no service
> registers it. `app.UseGlobalExceptionHandler()` must be the very first middleware in every
> service's `Program.cs` (it wraps everything registered after it — see
> `.claude/instructions/Dotnet.instructions.md` pitfall #42), immediately followed by
> `UseCorrelationId()` then `UseLocalization()`. See `Doc/LOCALIZATION_GUIDE.md`'s "Doc correction
> (August 2026)" note for the fuller writeup of this same dead-code/real-code distinction, and
> `Doc/OBSERVABILITY_GUIDE.md` for the correlation-ID pipeline-position detail.
>
> Both paths produce the **same** RFC 7807 `ProblemDetails` shape shown below (`status`/`title`/
> `detail`/`instance`/`traceId`/`errors`) — `SharedValidationFilter<T>` builds it inline for
> endpoint-filter-level validation failures (minimal-API model binding already succeeded, so there's
> a concrete `T` to validate before the handler runs); `GlobalExceptionHandler.CreateProblemDetails`
> builds the identical shape for a `FluentValidation.ValidationException` thrown deeper in the
> pipeline (e.g. by `ValidationBehavior<TRequest, TResponse>` in
> `IhsanDev.Shared.Application/Common/Behaviors/`). The "Service-Level Wrappers" list below was also
> stale — `ValidationFilter<T> : SharedValidationFilter<T>` wrappers now exist in **all** services
> that expose validated commands, not just the original four: Identity, Notification, Tenant,
> FileManager, Category, Translation, Backup, and the Nasheed/PolySnap apps.

## Overview

Implemented a **centralized, localized validation error handling solution** across all microservices. This ensures consistent error responses with proper localization support when validation errors occur.

## Problem Solved

Previously, each service had its own validation error handling with hardcoded titles and details. Now all services share:

- **Consistent error structure** across all APIs
- **Localized error messages** in English and Arabic
- **Single source of truth** for validation error formatting

## Architecture

### 1. Shared Infrastructure Layer

**File**: `src/Shared/IhsanDev.Shared.Infrastructure/Filters/ValidationFilter.cs`

Created `SharedValidationFilter<T>` class that:

- Validates incoming requests using FluentValidation
- Returns localized error responses with:
  - Title: `LocalizationKeys.Exceptions.BadRequest`
  - Detail: `LocalizationKeys.Exceptions.ValidationError`
  - Structured error details with property-level validation messages
- Uses camelCase property names in error responses

### 2. Service-Level Wrappers

Each service now has a thin wrapper that inherits from the shared filter:

**Identity Service**: `src/Services/Identity/Identity.API/Filters/ValidationFilter.cs`

```csharp
public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
```

**Notification Service**: `src/Services/Notification/Notification.API/Filters/ValidationFilter.cs`

```csharp
public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
```

**Tenant Service**: `src/Services/Tenant/Tenant.API/Filters/ValidationFilter.cs`

```csharp
public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
```

**FileManager Service**: `src/Services/FileManager/FileManager.API/Filters/ValidationFilter.cs`

```csharp
public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
```

**Also present (identical one-line wrapper pattern, added since this doc was first written):**
Category (`Category.API/Filters/ValidationFilter.cs`), Translation (`Translation.API/Filters/ValidationFilter.cs`),
Backup (`Backup.API/Filters/ValidationFilter.cs`), Nasheed (`Nasheed.API/Filters/ValidationFilter.cs`),
and PolySnap (`PolySnap.API/Filters/ValidationFilter.cs`).

### 3. Endpoint Integration

#### Identity Service

- All `/api/auth/*` endpoints use `AddEndpointFilter<ValidationFilter<Command>>()`
- Example: Register endpoint uses `AddEndpointFilter<ValidationFilter<RegisterCommand>>()`

#### Tenant Service

- Create Tenant: `AddEndpointFilter<ValidationFilter<CreateTenantCommand>>()`
- Update Tenant: `AddEndpointFilter<ValidationFilter<UpdateTenantCommand>>()`

#### Notification Service

- Send Notification: `AddEndpointFilter<ValidationFilter<SendNotificationCommand>>()`
- Get Queue Items: `AddEndpointFilter<ValidationFilter<GetQueueItemsCommand>>()`

## Error Response Format

All validation errors now return this consistent structure:

```json
{
  "status": 400,
  "title": "Bad Request",
  "detail": "One or more validation errors occurred",
  "instance": "/api/auth/register",
  "traceId": "0HN2N7CDGQJ5F:00000001",
  "errors": {
    "email": ["Email is required", "Invalid email address"],
    "password": ["Password must be at least 8 characters long"]
  }
}
```

## Localization Keys

Two new localization keys were added to support this feature:

**English** (`en.json`):

- `exception_validation_error`: "One or more validation errors occurred"
- `exception_unexpected_error`: "An unexpected error occurred. Please try again later."

**Arabic** (`ar.json`):

- `exception_validation_error`: "حدث خطأ واحد أو أكثر من أخطاء التحقق"
- `exception_unexpected_error`: "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى لاحقاً."

## Benefits

✅ **Consistency**: All services return the same error format
✅ **Localization**: Errors respond in user's preferred language
✅ **DRY Principle**: Single implementation shared across all services
✅ **Easy Maintenance**: Update error format once, applies everywhere
✅ **Structured Errors**: Clients can parse errors by property for targeted feedback

## Files Modified

### Created

- `src/Shared/IhsanDev.Shared.Infrastructure/Filters/ValidationFilter.cs` (SharedValidationFilter)

### Modified

- `src/Services/Identity/Identity.API/Filters/ValidationFilter.cs` (now inherits from shared)
- `src/Services/Notification/Notification.API/Filters/ValidationFilter.cs` (new)
- `src/Services/Tenant/Tenant.API/Filters/ValidationFilter.cs` (new)
- `src/Services/FileManager/FileManager.API/Filters/ValidationFilter.cs` (new)
- `src/Services/Notification/Notification.API/Extensions/EndpointMappingExtensions.cs` (added filter to endpoints)
- `src/Services/Tenant/Tenant.API/Extensions/EndpointMappingExtensions.cs` (added filter to endpoints)
- `src/Shared/IhsanDev.Shared.Application/Localization/LocalizationKeys.cs` (new keys)
- `src/Shared/IhsanDev.Shared.Application/Resources/Localization/en.json` (new translations)
- `src/Shared/IhsanDev.Shared.Application/Resources/Localization/ar.json` (new translations)

## How to Use

When creating a new endpoint with validation:

```csharp
var command = new YourCommand(...);

group.MapPost("/your-endpoint", YourHandler)
    .Produces<YourResponse>(200)
    .ProducesValidationProblem()
    .AddEndpointFilter<ValidationFilter<YourCommand>>();  // Add this line
```

The `ValidationFilter<YourCommand>` will automatically:

1. Validate the command using FluentValidation
2. Return localized error responses if validation fails
3. Proceed to handler if validation passes

## Testing

Test the implementation by sending invalid data to any endpoint with the validation filter:

```bash
# Example: Invalid registration with missing fields
POST /api/auth/register
Content-Type: application/json

{
  "email": "invalid-email"
}

# Response (400 Bad Request) - will be localized based on language settings
{
  "status": 400,
  "title": "Bad Request",
  "detail": "One or more validation errors occurred",
  "errors": {
    "email": ["Invalid email address"],
    "password": ["Password is required"],
    "firstName": ["FirstName is required"]
  }
}
```

---

**Version**: 2.0  
**Date**: January 15, 2026  
**Implementation Status**: ✅ Complete - All field names and validation messages fully localized

---

## January 15, 2026 Update

### What's New:

✅ **Complete Field Name Localization**

- Added 45 field name constants in `LocalizationKeys.Fields`
- All validators now use `L(LocalizationKeys.Fields.FieldName)` instead of hardcoded strings
- Example: `"Email"` → `L(LocalizationKeys.Fields.Email)`

✅ **Format Validation Message Localization**

- Added 7 specific validation message keys:
  - `FirstNameLettersOnly` - "First name must contain only letters"
  - `LastNameLettersOnly` - "Last name must contain only letters"
  - `VerificationCodeLength` - "Verification code must be {0} characters"
  - `VerificationCodeAlphanumeric` - "Verification code must contain only letters and digits"
  - `VerificationCodeDigitsOnly` - "Verification code must contain only digits"
  - `GroupInvalid` - "Group must be one of: user-uploads, profile-pictures, documents, attachments"
  - `SortColumnInvalid` - "Sort column must be one of: uploadedAt, fileName, fileSize"

✅ **Total Localization Keys: 109** (45 fields + 34 validation + 30 others)

✅ **Zero Hardcoded Text** - Comprehensive verification confirmed no remaining hardcoded field names or validation messages

### Example Before/After:

**Before (Nov 2025):**

```csharp
RuleFor(x => x.Email)
    .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Email"))
    .EmailAddress().WithMessage(L(LocalizationKeys.Validation.EmailInvalid));
```

**After (Jan 15, 2026):**

```csharp
RuleFor(x => x.Email)
    .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, L(LocalizationKeys.Fields.Email)))
    .EmailAddress().WithMessage(L(LocalizationKeys.Validation.EmailInvalid));
```
