# TraceId Logging & Validation Error Logging - Summary

**Date**: January 15, 2026  
**Issues**:

- TraceId was being sent in HTTP responses but not recorded in log files
- Validation failures were not logged (requests never reached MediatR)
- MediatR logs showed localization keys instead of localized messages

**Status**: ✅ All Fixed

## Problem Description

When errors occurred in the application, the HTTP response included a `traceId` field for request correlation:

```json
{
  "status": 400,
  "title": "Bad Request",
  "detail": "Validation failed",
  "traceId": "0HNIJVSH6HAU1:00000001",
  "errors": { ... }
}
```

However, when checking the log files in `Logs/*/project-*.log`, the traceId was **nowhere to be found**. This made it impossible to correlate HTTP responses with log entries, making debugging significantly harder.

### Root Cause

The logging system had two separate paths:

1. **HTTP Response Path** ✅ (Working)

   - `GlobalExceptionHandlingMiddleware` captured `context.TraceIdentifier`
   - `ValidationFilter` captured `context.TraceIdentifier`
   - TraceId was included in JSON responses sent to clients

2. **File Logging Path** ❌ (Missing TraceId)
   - `LoggerManager` wrote logs to files
   - `FormatLogMessage()` method only included: timestamp, log level, message, exception
   - **No traceId was captured or written**

The traceId existed in `HttpContext.TraceIdentifier` but was never passed to the logging system.

## Solution Implemented

### 1. Updated `ILoggerManager` Interface

Added optional `traceId` parameter to all log methods:

```csharp
public interface ILoggerManager
{
    void LogInfo(string message, string? serviceName = null, string? traceId = null);
    void LogWarn(string message, string? serviceName = null, string? traceId = null);
    void LogDebug(string message, string? serviceName = null, string? traceId = null);
    void LogError(string message, string? serviceName = null, string? traceId = null);
    void LogError(Exception exception, string message, string? serviceName = null, string? traceId = null);
}
```

### 2. Updated `LoggerManager` Implementation

Modified internal methods to capture and format traceId:

```csharp
private void Log(LogLevel logLevel, string message, string? serviceName = null, Exception? exception = null, string? traceId = null)
{
    // ... existing code ...
    WriteLogToFile(logLevel, contextualMessage, exception, traceId);
}

private static string FormatLogMessage(LogLevel logLevel, string message, Exception? exception = null, string? traceId = null)
{
    var traceIdPart = !string.IsNullOrWhiteSpace(traceId) ? $" | TraceId: {traceId}" : string.Empty;
    var logEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}]{traceIdPart} {message}";
    // ... rest of formatting ...
}
```

### 3. Created `ITraceIdProvider` Service

Created a clean architecture abstraction to access `HttpContext.TraceIdentifier`:

**Interface** (`IhsanDev.Shared.Application`):

```csharp
public interface ITraceIdProvider
{
    string? GetTraceId();
}
```

**Implementation** (`IhsanDev.Shared.Infrastructure`):

```csharp
public class TraceIdProvider : ITraceIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TraceIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetTraceId()
    {
        return _httpContextAccessor.HttpContext?.TraceIdentifier;
    }
}
```

### 4. Updated `LoggingBehavior` to Use TraceIdProvider and Localization

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILoggerManager _loggerManager;
    private readonly ITraceIdProvider _traceIdProvider;
    private readonly ILocalizationService _localizationService;

    public LoggingBehavior(
        ILoggerManager loggerManager,
        ITraceIdProvider traceIdProvider,
        ILocalizationService localizationService)
    {
        _loggerManager = loggerManager;
        _traceIdProvider = traceIdProvider;
        _localizationService = localizationService;
    }

    public async Task<TResponse> Handle(...)
    {
        // Get the SAME traceId that's used in HTTP responses
        var traceId = _traceIdProvider.GetTraceId();

        _loggerManager.LogInfo($"Handling {requestName}", "MediatR", traceId);

        try
        {
            var response = await next();
            _loggerManager.LogInfo($"Handled {requestName} in {elapsed}ms", "MediatR", traceId);
            return response;
        }
        catch (AppException appException)
        {
            // Localize the exception message for logging (same as HTTP responses)
            var localizedMessage = _localizationService.GetString(appException.Message);
            _loggerManager.LogWarn(
                $"Business exception in {requestName} after {elapsed}ms: {localizedMessage}",
                "MediatR",
                traceId);
            throw;
        }
    }
}
```

**Critical Changes**:

- Uses **exact same** traceId as HTTP responses (`HttpContext.TraceIdentifier`), not `Activity.Current.Id`
- **Localizes exception messages** in logs to match HTTP responses (no more raw keys like `exception_invalid_credentials`)

## Before vs After

### Before (Log File)

```log
2026-01-15 05:52:16.305 [Error] [MediatR] Unexpected error handling GetAllActiveTenantsQuery after 4ms
Exception: FluentValidation.ValidationException: Validation failed:
 -- PageNumber: Page number must be greater than 0 Severity: Error
```

**Problem**: No way to find this log entry if user reports error with traceId `0HNIJVSH6HAU1:00000001`

### After (Log File)

```log
2026-01-15 05:52:16.305 [Error] | TraceId: 0HNIJVSH6HAU1:00000001 [MediatR] Unexpected error handling GetAllActiveTenantsQuery after 4ms
Exception: FluentValidation.ValidationException: Validation failed:
 -- PageNumber: Page number must be greater than 0 Severity: Error
```

**Solution**: Now you can search log files for `0HNIJVSH6HAU1:00000001` and find all related entries!

### 5. Updated `ValidationFilter` to Log Validation Failures

**Problem**: When validation failed at the endpoint filter level, the request never reached MediatR, so LoggingBehavior never ran. This meant NO LOGS were created for validation failures.

**Solution**: SharedValidationFilter now logs all validation failures with traceId:

```csharp
public class SharedValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(...)
    {
        var validationResult = await _validator.ValidateAsync(argumentToValidate);

        if (!validationResult.IsValid)
        {
            var loggerManager = context.HttpContext.RequestServices.GetRequiredService<ILoggerManager>();
            var traceId = context.HttpContext.TraceIdentifier;

            // Log validation failure with traceId for tracking
            var requestType = typeof(T).Name;
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            loggerManager.LogWarn(
                $"Validation failed for {requestType}: {errors}",
                "ValidationFilter",
                traceId);

            return Results.BadRequest(problemDetails);
        }
    }
}
```

**Result**: Now validation failures are logged BEFORE returning 400 BadRequest, so you can track them in log files.

### 6. Updated `LoggingExtensions` to Auto-Register Dependencies

```csharp
public static IServiceCollection AddCustomLogging(...)
{
    // Register HttpContextAccessor (required by TraceIdProvider)
    services.AddHttpContextAccessor();

    // ... register LoggerManager and TraceIdProvider ...
}
```

**Benefit**: All services calling `AddCustomLogging()` automatically get `IHttpContextAccessor` registered, preventing DI errors.

## Benefits

✅ **Request Correlation**: Match HTTP error responses with exact log entries  
✅ **Easier Debugging**: Search logs by traceId provided by users/monitoring  
✅ **Distributed Tracing**: TraceId can be passed to other microservices  
✅ **Validation Logging**: All validation failures now logged with traceId  
✅ **Localized Logs**: Exception messages in logs match HTTP responses (no raw keys)  
✅ **Backward Compatible**: TraceId is optional - existing code works without changes  
✅ **Zero Performance Impact**: TraceId only added when available  
✅ **Auto-Configuration**: HttpContextAccessor automatically registered with logging

## How to Use

### Automatic (MediatR Handlers)

TraceId is **automatically** included when using MediatR commands/queries:

```csharp
// No code changes needed - traceId automatically captured
var result = await _mediator.Send(new GetUserQuery(userId));
```

### Manual (Custom Logging)

For code with direct `HttpContext` access:

```csharp
public async Task<IResult> MyEndpoint(HttpContext context, ILoggerManager logger)
{
    var traceId = context.TraceIdentifier;
    logger.LogInfo("Processing request", "MyService", traceId);

    try
    {
        // ... your code ...
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Request failed", "MyService", traceId);
        throw;
    }
}
```

### Without HttpContext

If you don't have access to `HttpContext`, simply omit the traceId:

```csharp
// Still works - traceId will be null/empty in logs
logger.LogInfo("Background job started", "BackgroundService");
```

## Files Modified

### Core Changes

1. ✅ [ILoggerManager.cs](../src/Shared/IhsanDev.Shared.Application/Common/Interfaces/ILoggerManager.cs) - Added traceId parameter
2. ✅ [LoggerManager.cs](../src/Shared/IhsanDev.Shared.Infrastructure/Services/Logging/LoggerManager.cs) - Implemented traceId capture and formatting
3. ✅ [ITraceIdProvider.cs](../src/Shared/IhsanDev.Shared.Application/Common/Interfaces/ITraceIdProvider.cs) - Interface for accessing traceId
4. ✅ [TraceIdProvider.cs](../src/Shared/IhsanDev.Shared.Infrastructure/Services/TraceIdProvider.cs) - Implementation using HttpContext.TraceIdentifier
5. ✅ [LoggingBehavior.cs](../src/Shared/IhsanDev.Shared.Application/Common/Behaviors/LoggingBehavior.cs) - Inject ITraceIdProvider and ILocalizationService
6. ✅ [ValidationFilter.cs](../src/Shared/IhsanDev.Shared.Infrastructure/Filters/ValidationFilter.cs) - Log validation failures with traceId
7. ✅ [LoggingExtensions.cs](../src/Shared/IhsanDev.Shared.Infrastructure/Extensions/LoggingExtensions.cs) - Auto-register HttpContextAccessor and TraceIdProvider

### Documentation Updates

7. ✅ [CUSTOM_LOGGER_USAGE.md](CUSTOM_LOGGER_USAGE.md) - Added traceId examples and documentation
8. ✅ [TRACEID_LOGGING_FIX_SUMMARY.md](TRACEID_LOGGING_FIX_SUMMARY.md) - This file

## Testing

After deploying this fix:

1. Make an API request that triggers an error
2. Note the `traceId` in the HTTP response
3. Search the log file for that traceId: `grep "0HNIJVSH6HAU1:00000001" Logs/*/project-*.log`
4. ✅ You should now find matching log entries!

## Migration Notes

**No breaking changes!** All existing code continues to work because:

- `traceId` parameter is optional (defaults to `null`)
- When `traceId` is `null`, logs look exactly like before (no extra field)
- Only when `traceId` is provided, it appears in the log

## Related Documentation

- [CUSTOM_LOGGER_USAGE.md](CUSTOM_LOGGER_USAGE.md) - Complete logging guide
- [CENTRALIZED_VALIDATION_ERROR_HANDLING.md](CENTRALIZED_VALIDATION_ERROR_HANDLING.md) - Validation errors include traceId
- [CENTRALIZED_VALIDATION_ERROR_HANDLING_QUICK_REFERENCE.md](CENTRALIZED_VALIDATION_ERROR_HANDLING_QUICK_REFERENCE.md) - Quick reference

## Example: Complete Request Flow

### Scenario: Register endpoint with validation failure

**Request**:

```bash
POST /api/auth/register
{
  "email": "user@example.com",
  "password": "weak",  # Missing uppercase & special char
  "firstName": "John",
  "lastName": "Doe"
}
```

**HTTP Response** (400 Bad Request):

```json
{
  "status": 400,
  "title": "طلب غير صالح",
  "detail": "حدث خطأ واحد أو أكثر من أخطاء التحقق",
  "instance": "/api/auth/register",
  "traceId": "0HNIK0HVNM5E4:00000001",
  "errors": {
    "password": [
      "يجب أن تحتوي كلمة المرور على حرف كبير واحد على الأقل",
      "يجب أن تحتوي كلمة المرور على حرف خاص واحد على الأقل"
    ]
  }
}
```

**Log File** (`Logs/Identity/project-2026-01-15.log`):

```log
2026-01-15 07:09:00.554 [Warning] | TraceId: 0HNIK0HVNM5E4:00000001 [ValidationFilter] Validation failed for RegisterCommand: Password: يجب أن تحتوي كلمة المرور على حرف كبير واحد على الأقل; Password: يجب أن تحتوي كلمة المرور على حرف خاص واحد على الأقل
```

✅ **Perfect match**: Same traceId, same localized messages!

### Scenario: Login with invalid credentials

**Request**:

```bash
POST /api/auth/login
{
  "email": "user@example.com",
  "password": "WrongPassword123!"
}
```

**HTTP Response** (401 Unauthorized):

```json
{
  "title": "وصول غير مصرح به",
  "status": 401,
  "detail": "البريد الإلكتروني أو كلمة المرور غير صحيحة",
  "instance": "/api/auth/login",
  "traceId": "0HNIK0HVNM5E4:00000003"
}
```

**Log File**:

```log
2026-01-15 07:06:03.588 [Information] | TraceId: 0HNIK0HVNM5E4:00000003 [MediatR] Handling LoginCommand
2026-01-15 07:06:03.625 [Warning] | TraceId: 0HNIK0HVNM5E4:00000003 [MediatR] Business exception in LoginCommand after 36ms: البريد الإلكتروني أو كلمة المرور غير صحيحة
```

✅ **Perfect match**: Same traceId, localized message (not `exception_invalid_credentials`)!

---

**Version**: 2.0  
**Last Updated**: January 15, 2026  
**Status**: Production-Ready
