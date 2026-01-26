# Frontend Error Interceptor Implementation Summary

**Date:** January 25, 2026  
**Related Frontend Docs:**

- `MicroservicesArchitecture-Web/Doc/FRONTEND_ERROR_INTERCEPTOR_GUIDE.md`
- `MicroservicesArchitecture-Web/Doc/FRONTEND_ERROR_INTERCEPTOR_QUICK_REFERENCE.md`

---

## 📋 Overview

A comprehensive Angular HTTP interceptor has been implemented in the frontend to handle all backend error responses. The interceptor automatically:

1. **Catches all HTTP errors** from backend services
2. **Parses error responses** (supports both ProblemDetails and ErrorResponse formats)
3. **Displays toast notifications** to users
4. **Handles authentication failures** (auto-redirects to login)
5. **Logs detailed error information** for debugging

---

## 🔗 Integration with Backend

The frontend error interceptor is **fully compatible** with both backend error handling approaches:

### Backend Error Sources

1. **GlobalExceptionHandler.cs** (Primary - IExceptionHandler)
   - Returns ASP.NET Core `ProblemDetails` format
   - Used by most services
   - Supports FluentValidation field-level errors

2. **GlobalExceptionHandlingMiddleware.cs** (Legacy - Middleware)
   - Returns custom `ErrorResponse` format
   - Still supported for backward compatibility

### Error Response Mapping

| Backend Exception     | Status | Frontend Handler     | User Experience               |
| --------------------- | ------ | -------------------- | ----------------------------- |
| BadRequestException   | 400    | handleBadRequest()   | Show validation errors        |
| ValidationException   | 400    | handleBadRequest()   | Show field-level errors       |
| UnauthorizedException | 401    | handleUnauthorized() | Toast + redirect to login     |
| ForbiddenException    | 403    | handleForbidden()    | Show "Access Denied"          |
| NotFoundException     | 404    | handleNotFound()     | Show "Not Found"              |
| ConflictException     | 409    | handleConflict()     | Show "Conflict"               |
| GeneralException      | 500    | handleServerError()  | Show "Server Error" + traceId |
| Network/Connection    | 0      | handleNetworkError() | Show "Connection Error"       |

---

## 📦 Error Response Format Support

### Format 1: ProblemDetails (ASP.NET Core Standard)

**Backend Source:** `GlobalExceptionHandler.cs`

```json
{
  "status": 400,
  "title": "Bad Request",
  "detail": "Invalid request data",
  "instance": "/api/users",
  "traceId": "00-abc123...",
  "errors": {
    "email": ["Email is required", "Invalid email format"],
    "password": ["Password must be at least 8 characters"]
  }
}
```

**Frontend Parsing:**

```typescript
interface IProblemDetails {
  status: number;
  title: string;
  detail: string;
  instance: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
```

---

### Format 2: ErrorResponse (Legacy)

**Backend Source:** `GlobalExceptionHandlingMiddleware.cs`

```json
{
  "statusCode": 404,
  "title": "Not Found",
  "message": "User not found",
  "localizationKey": "Exceptions.UserNotFound",
  "traceId": "00-def456...",
  "timestamp": "2026-01-25T10:30:00Z"
}
```

**Frontend Parsing:**

```typescript
interface IErrorResponse {
  statusCode: number;
  title: string;
  message: string;
  localizationKey: string;
  traceId: string;
  timestamp: string;
}
```

---

## 🎯 Key Features

### 1. Automatic Format Detection

The interceptor automatically detects which format the backend is using:

```typescript
function parseErrorResponse(
  error: HttpErrorResponse,
): IProblemDetails | IErrorResponse | null {
  if (!error.error) return null;

  // Check if it's ProblemDetails format (has 'detail' property)
  if ("detail" in error.error) {
    return error.error as IProblemDetails;
  }

  // Check if it's ErrorResponse format (has 'message' property)
  if ("message" in error.error) {
    return error.error as IErrorResponse;
  }

  return null;
}
```

### 2. Validation Error Display

FluentValidation errors are displayed with field names:

**Backend Response:**

```json
{
  "errors": {
    "email": ["Email is required", "Invalid email format"],
    "password": ["Password must be at least 8 characters"],
    "name": ["Name is required"]
  }
}
```

**Frontend Display:**

```
🔴 Validation Failed (3 fields)
email: Email is required, Invalid email format
password: Password must be at least 8 characters
name: Name is required
```

### 3. Trace ID Propagation

Trace IDs from backend are:

- **Logged to console** for developer debugging
- **Shown in toast** (server errors only) for user reporting
- **Correlated with backend logs** for end-to-end tracing

### 4. Authentication Handling

401 Unauthorized errors automatically:

1. Show toast notification
2. Clear authentication tokens
3. Redirect to login with return URL: `/auth/login?returnUrl=/current-page`

---

## 📁 File Locations

### Frontend Implementation

- **Interceptor:** `MicroservicesArchitecture-Web/libs/shared/src/lib/interceptors/error.interceptor.ts`
- **Registration:** `MicroservicesArchitecture-Web/apps/admin/src/app/app.config.ts`
- **Export:** `MicroservicesArchitecture-Web/libs/shared/src/index.ts`

### Backend Error Handlers

- **Primary Handler:** `src/Shared/IhsanDev.Shared.Infrastructure/Middleware/GlobalExceptionHandler.cs`
- **Legacy Middleware:** `src/Shared/IhsanDev.Shared.Infrastructure/Middleware/GlobalExceptionHandlingMiddleware.cs`
- **Exception Types:** `src/Shared/IhsanDev.Shared.Application/Exceptions/AppException.cs`

---

## 🔄 Error Flow Diagram

```
Frontend HTTP Request
        ↓
[Token Interceptor] ← Adds JWT token
        ↓
Backend API Endpoint
        ↓
[Exception Occurs]
        ↓
Backend Exception Handler
   ├─ GlobalExceptionHandler (ProblemDetails)
   └─ GlobalExceptionHandlingMiddleware (ErrorResponse)
        ↓
HTTP Response with Error
        ↓
[Error Interceptor] ← Catches error
        ↓
Parse Response Format
        ↓
Route to Handler (based on status code)
        ↓
┌───────────┬──────────────┬──────────────┐
│Show Toast │Redirect Login│Log to Console│
└───────────┴──────────────┴──────────────┘
        ↓
User sees error notification
Developer sees console log with trace ID
```

---

## 🧪 Testing Compatibility

### Test Backend Error Responses

Both formats work seamlessly:

**Test 1: ProblemDetails Format**

```typescript
httpMock.expectOne("/api/users").flush(
  {
    status: 400,
    title: "Bad Request",
    detail: "Invalid email",
    errors: { email: ["Invalid format"] },
  },
  { status: 400, statusText: "Bad Request" },
);

// ✅ Frontend shows: "Validation Failed (1 field) - email: Invalid format"
```

**Test 2: ErrorResponse Format**

```typescript
httpMock.expectOne("/api/users").flush(
  {
    statusCode: 404,
    title: "Not Found",
    message: "User not found",
    traceId: "00-abc123",
  },
  { status: 404, statusText: "Not Found" },
);

// ✅ Frontend shows: "Not Found - User not found"
```

---

## ✅ Benefits

1. **Consistent UX** - All errors display uniformly across the app
2. **Reduced Code** - No need to handle errors in every service
3. **Better Debugging** - Trace IDs link frontend → backend logs
4. **Localization Support** - Uses backend localized messages
5. **Type Safety** - TypeScript interfaces for both error formats
6. **Future-Proof** - Supports both legacy and new error formats

---

## 🔧 Migration Notes

### For New Services

✅ Use `GlobalExceptionHandler` (IExceptionHandler) for all new services  
✅ Return `ProblemDetails` format  
✅ Frontend interceptor handles it automatically

### For Existing Services

✅ Both `ProblemDetails` and `ErrorResponse` formats work  
✅ No frontend changes needed when updating backend error handler  
✅ Gradual migration supported

---

## 📚 Related Documentation

### Frontend

- **Full Guide:** `MicroservicesArchitecture-Web/Doc/FRONTEND_ERROR_INTERCEPTOR_GUIDE.md`
- **Quick Reference:** `MicroservicesArchitecture-Web/Doc/FRONTEND_ERROR_INTERCEPTOR_QUICK_REFERENCE.md`

### Backend

- **Validation Errors:** `Doc/CENTRALIZED_VALIDATION_ERROR_HANDLING.md`
- **Localization:** `Doc/COMPLETE_LOCALIZATION_MIGRATION_SUMMARY.md`
- **Exception Handling:** `Doc/CENTRALIZED_VALIDATION_ERROR_HANDLING_QUICK_REFERENCE.md`

---

## 🎓 Usage Example

### Backend Service (Identity)

```csharp
public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDtoIncludesToken>
{
    public async Task<UserDtoIncludesToken> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, ct);

        // Backend throws localized exception
        user.ThrowIfNull(LocalizationKeys.Exceptions.UserNotFound);

        // ...
    }
}
```

### Frontend Component

```typescript
export class LoginComponent {
  private readonly _authService = inject(AuthService);

  onSubmit(): void {
    // No error handling needed - interceptor shows toast automatically
    this._authService.login(this.loginForm.value).subscribe({
      next: (response) => {
        toast.success("Login successful");
        this.router.navigate(["/dashboard"]);
      },
      error: () => {
        // Error already handled by interceptor
        this.isLoading.set(false);
      },
    });
  }
}
```

**User Experience:**

- ✅ Wrong password → Shows: "Not Found - User not found"
- ✅ Invalid email format → Shows: "Validation Failed (1 field) - email: Invalid format"
- ✅ Network error → Shows: "Connection Error - Unable to connect to server"

---

**Version:** 1.0  
**Last Updated:** January 25, 2026  
**Status:** Production Ready ✅
