# Custom Logger Implementation

This document demonstrates how to use the custom logger system in the microservices architecture.

## Overview

The custom logger system consists of:

- **ILoggerManager**: Interface for custom logging operations
- **LoggerManager**: Implementation with file logging and colored console output
- **LoggingExtensions**: Extension methods for easy registration
- **Enhanced LoggingBehavior**: MediatR pipeline behavior using the custom logger

## Features

- ✅ **Colored Console Output**: Different colors for different log levels
- ✅ **File Logging**: Automatic daily log file rotation
- ✅ **Service Context**: Optional service name for multi-service logging
- ✅ **TraceId Support**: Automatic request correlation tracking
- ✅ **Thread-Safe**: Safe for concurrent operations
- ✅ **Exception Support**: Enhanced exception logging with stack traces
- ✅ **MediatR Integration**: Automatic request/response logging with traceId

## Configuration

### 1. Add Logging Configuration to appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "FilePath": "Logs"
  }
}
```

### 2. Register the Logger in Program.cs

```csharp
// Register custom logging with service name
builder.Services.AddCustomLogging(builder.Configuration, "Identity");

// OR register with explicit path
builder.Services.AddCustomLogging("C:\\MyApp\\Logs");
```

## Usage Examples

### 1. In Controllers/Handlers

```csharp
public class UserHandler
{
    private readonly ILoggerManager _logger;

    public UserHandler(ILoggerManager logger)
    {
        _logger = logger;
    }

    public async Task<User> GetUserAsync(int id)
    {
        _logger.LogInfo($"Getting user with ID: {id}", "UserService");

        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarn($"User with ID {id} not found", "UserService");
                return null;
            }

            _logger.LogInfo($"Successfully retrieved user: {user.Email}", "UserService");
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving user with ID: {id}", "UserService");
            throw;
        }
    }
}
```

### 2. In Services

```csharp
public class EmailService
{
    private readonly ILoggerManager _logger;

    public EmailService(ILoggerManager logger)
    {
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject)
    {
        _logger.LogInfo($"Sending email to: {to}", "EmailService");

        try
        {
            // Email sending logic
            await SendAsync(to, subject);
            _logger.LogInfo($"Email sent successfully to: {to}", "EmailService");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email to: {to}", "EmailService");
            throw;
        }
    }
}
```

### 3. In Middleware

```csharp
public class CustomMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILoggerManager _logger;

    public CustomMiddleware(RequestDelegate next, ILoggerManager logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestPath = context.Request.Path;
        _logger.LogInfo($"Processing request: {requestPath}", "Middleware");

        try
        {
            await _next(context);
            _logger.LogInfo($"Request completed: {requestPath}", "Middleware");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Request failed: {requestPath}", "Middleware");
            throw;
        }
    }
}
```

## Log Output Examples

### Console Output

```
2024-10-14 15:30:15 [Information] [Identity] Handling LoginCommand
2024-10-14 15:30:15 [Information] [UserService] Getting user with ID: 123
2024-10-14 15:30:16 [Information] [UserService] Successfully retrieved user: user@example.com
2024-10-14 15:30:16 [Information] [Identity] Handled LoginCommand in 1250ms
```

### File Output (Logs/Identity/project-2024-10-14.log)

```
2024-10-14 15:30:15.123 [Information] | TraceId: 0HNIJVSH6HAU1:00000001 [Identity] Handling LoginCommand
2024-10-14 15:30:15.456 [Information] [UserService] Getting user with ID: 123
2024-10-14 15:30:16.789 [Information] [UserService] Successfully retrieved user: user@example.com
2024-10-14 15:30:16.890 [Information] | TraceId: 0HNIJVSH6HAU1:00000001 [Identity] Handled LoginCommand in 1250ms
```

**Note**: TraceId is automatically included in logs when available from the HTTP request context. This allows you to correlate log entries with HTTP responses and track requests across services.

## Log Levels and Colors

| Level           | Color   | When to Use                                         |
| --------------- | ------- | --------------------------------------------------- |
| **Debug**       | Blue    | Detailed diagnostic information                     |
| **Information** | Green   | General application flow                            |
| **Warning**     | Yellow  | Unexpected situations that don't stop the app       |
| **Error**       | Red     | Error events that allow the app to continue         |
| **Critical**    | Magenta | Critical errors that may cause the app to terminate |

## File Structure

The logger creates daily log files in the following structure:

```
Logs/
├── Identity/
│   ├── project-2024-10-14.log
│   ├── project-2024-10-15.log
│   └── ...
├── UserService/
│   ├── project-2024-10-14.log
│   └── ...
└── ...
```

## Advanced Features

### Exception Logging

The logger automatically includes full exception details when logging errors:

```csharp
try
{
    // Some operation
}
catch (Exception ex)
{
    _logger.LogError(ex, "Operation failed", "ServiceName");
    // Logs both the message and full exception stack trace
}
```

### MediatR Integration

The custom LoggingBehavior automatically logs all MediatR requests with traceId for request correlation and localizes exception messages:

```csharp
// Automatically logged with traceId and localized messages:
// [Information] | TraceId: 0HNIK0HVNM5E4:00000003 [MediatR] Handling LoginCommand
// [Warning] | TraceId: 0HNIK0HVNM5E4:00000003 [MediatR] Business exception in LoginCommand after 36ms: البريد الإلكتروني أو كلمة المرور غير صحيحة
var result = await _mediator.Send(new LoginCommand { Email = "test@example.com" });
```

**Key Features**:

- ✅ Automatic traceId inclusion for request correlation
- ✅ Localized error messages (matches HTTP responses)
- ✅ No code changes needed in handlers
- ✅ Performance metrics (execution time)

### Validation Logging

Validation failures are automatically logged at the ValidationFilter level with traceId:

```csharp
// When validation fails, automatically logged as:
// [Warning] | TraceId: 0HNIK0HVNM5E4:00000001 [ValidationFilter] Validation failed for RegisterCommand: Password: يجب أن تحتوي كلمة المرور على حرف كبير واحد على الأقل; Password: يجب أن تحتوي كلمة المرور على حرف خاص واحد على الأقل

// User receives 400 BadRequest with matching traceId
// No need to manually log validation errors
```

**Benefits**:

- ✅ All validation failures appear in logs
- ✅ TraceId matches HTTP response for easy correlation
- ✅ Localized error messages
- ✅ Works across all microservices using SharedValidationFilter

### Manual TraceId Logging

You can also manually pass traceId when logging from contexts with HttpContext access:

```csharp
public class UserEndpoint
{
    private readonly ILoggerManager _logger;

    public async Task<IResult> HandleRequest(HttpContext context)
    {
        var traceId = context.TraceIdentifier;
        _logger.LogInfo("Processing user request", "UserService", traceId);
        // This will include the traceId in the log file
    }
}
```

## Migration from Standard ILogger

Replace your existing ILogger usage:

```csharp
// OLD
private readonly ILogger<UserService> _logger;
_logger.LogInformation("User created: {UserId}", userId);

// NEW
private readonly ILoggerManager _logger;
_logger.LogInfo($"User created: {userId}", "UserService");
```

## Thread Safety

The LoggerManager is thread-safe and can be safely used in concurrent scenarios. File writing is synchronized using locks to prevent data corruption.
