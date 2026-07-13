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
- ✅ **TraceId = X-Correlation-Id**: The `TraceId` field in every log line is the `X-Correlation-Id` from the HTTP request — the same ID the client receives in the response header, enabling end-to-end grep across all services
- ✅ **Non-Blocking**: `LogInfo`/`LogWarn`/`LogDebug`/`LogError` never touch the console or disk on the calling thread — they format a record and hand it off to a background writer, so logging never adds request latency
- ✅ **Bounded, severity-aware queues**: Information/Debug and Warning/Error/Critical are queued separately. The Information/Debug queue holds up to 20,000 pending entries and drops the *newest* entry if it ever fills up (a warning prints every 1,000th drop). The Warning/Error/Critical queue holds up to 100,000 and, if it ever fills (e.g. an error-per-request incident sustained long enough), evicts the *oldest* queued entry to make room for the newest — so a real outage sheds old errors and keeps recent ones instead of the logger's own memory growing without limit. Every priority-queue eviction logs immediately (not sampled); low-priority drops sample every 1,000th. Both are exported as the `logger.entries.dropped` OTel counter (tagged `channel=low_priority`/`channel=priority`) via the existing Prometheus pipeline.
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
2024-10-14 15:30:15.123 [Information] | TraceId: f3a2b1c4-d5e6-7890-abcd-ef1234567890 [MediatR] Handling LoginCommand
2024-10-14 15:30:15.456 [Information] [UserService] Getting user with ID: 123
2024-10-14 15:30:16.789 [Information] [UserService] Successfully retrieved user: user@example.com
2024-10-14 15:30:16.890 [Information] | TraceId: f3a2b1c4-d5e6-7890-abcd-ef1234567890 [MediatR] Handled LoginCommand in 1250ms
```

**Note**: `TraceId` is the `X-Correlation-Id` from the HTTP request (read by `CorrelationIdMiddleware`). The same ID is echoed back to the client in the `X-Correlation-Id` response header. If no header was sent, a new UUID is auto-generated. Background tasks (no HTTP context) produce entries with no `TraceId` part — this is expected behaviour.

**Grep across all services for one request:**

```powershell
Select-String -Path "C:\...\Logs\*\*.log" -Pattern "f3a2b1c4-d5e6-7890-abcd-ef1234567890"
```

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
// Automatically logged with traceId (= X-Correlation-Id) and localized messages:
// [Information] | TraceId: f3a2b1c4-d5e6-7890-abcd-ef1234567890 [MediatR] Handling LoginCommand
// [Warning] | TraceId: f3a2b1c4-d5e6-7890-abcd-ef1234567890 [MediatR] Business exception in LoginCommand after 36ms: البريد الإلكتروني أو كلمة المرور غير صحيحة
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
// [Warning] | TraceId: f3a2b1c4-d5e6-7890-abcd-ef1234567890 [ValidationFilter] Validation failed for RegisterCommand: Password: يجب أن تحتوي كلمة المرور على حرف كبير واحد على الأقل; Password: يجب أن تحتوي كلمة المرور على حرف خاص واحد على الأقل

// User receives 400 BadRequest with matching traceId
// No need to manually log validation errors
```

**Benefits**:

- ✅ All validation failures appear in logs
- ✅ TraceId matches HTTP response for easy correlation
- ✅ Localized error messages
- ✅ Works across all microservices using SharedValidationFilter

### Manual TraceId Logging

You can also manually pass traceId when logging from contexts with HttpContext access. Always read from `HttpContext.Items["CorrelationId"]` — not `TraceIdentifier` — so the value matches what the client sees in the response header:

```csharp
public class UserEndpoint
{
    private readonly ILoggerManager _logger;

    public async Task<IResult> HandleRequest(HttpContext context)
    {
        // Read the X-Correlation-Id stored by CorrelationIdMiddleware
        var traceId = context.Items["CorrelationId"]?.ToString();
        _logger.LogInfo("Processing user request", "UserService", traceId);
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

## Thread Safety and Performance (rewritten July 2026)

`LoggerManager` is registered as a singleton and safe to call concurrently from any number of requests. Calling code (`LogInfo`, `LogWarn`, etc.) only formats a message and enqueues it onto one of two in-memory `System.Threading.Channels.Channel` instances (Information/Debug vs Warning/Error/Critical — see Features above) — no lock, no I/O, returns immediately. A single background task drains the priority channel first, then the low-priority one, performing the actual `Console.WriteLine` and file append, keeping the day's file `StreamWriter` open across calls (only reopening when the date rolls over) instead of opening/writing/closing per line.

**Why two channels, not one**: an incident that produces an Error per request (a DB outage, a failing downstream dependency) hits at full request rate for as long as it lasts — exactly the moment the logger itself must not become part of the outage via unbounded memory growth, and exactly the moment you can least afford to lose the error entries a single shared bounded channel would drop indiscriminately. Splitting the channels means Information/Debug (high volume, low individual value) can be dropped under load while Warning/Error/Critical get a much larger bound and only shed their *oldest* entries, never silently vanishing outright.

**A non-obvious gotcha this relies on**: `Channel<T>.Writer.TryWrite` returns `true` unconditionally under `BoundedChannelFullMode.DropWrite`/`DropOldest`/`DropNewest` — even when the item is silently discarded — so there is no way to detect (or count) a drop from the return value with those modes. Both channels here use `BoundedChannelFullMode.Wait` instead (which does reliably return `false` when full) and implement the drop/evict policy manually in code, specifically so the drop counters below are real, not silently-always-zero.

**Reader concurrency**: the low-priority channel is created with `SingleReader = true` — only the background writer task ever calls `Reader.TryRead`, since its overflow policy is "just don't write the new entry," not "read one out first." The priority channel is created with `SingleReader = false` instead: its evict-oldest overflow policy means `EnqueuePriority` calls `Reader.TryRead(out _)` directly from whatever request thread hit a full channel, which can happen concurrently with the background task's own reads on the same reader — `SingleReader = true` would (incorrectly) tell the channel only one thread ever reads from it.

**⚠️ This replaced an earlier implementation that took a `lock` and synchronously opened, wrote, and closed a `FileStream` on the *calling* thread for every single log call.** Since `LoggingBehavior` calls this twice per MediatR request, every authenticated request in every service serialized on that one lock — invisible at low traffic, but under sustained load (k6 testing, July 2026) it became the dominant bottleneck: p95 latency of 5-6 seconds despite 100% correctness, low CPU, and healthy Postgres/Redis, because request threads were blocked waiting on the lock and disk I/O rather than doing real work. The rewrite above fixed it — same test afterward showed p95 dropping to **4.73ms**. See `Doc/LOAD_TESTING_GUIDE.md` for the full investigation and `.claude/instructions/Dotnet.instructions.md` pitfall #13 for the general lesson (never block the calling thread in a component invoked on every request).

If the process crashes or is killed before the background writer drains, queued-but-not-yet-written log lines can be lost — acceptable for this system's dev/logging use case. `Error`/`Warning`/`Critical` entries are flushed to disk immediately after being written; `Information`/`Debug` entries flush on the next file rotation or on graceful shutdown (`LoggerManager` implements both `IAsyncDisposable` and `IDisposable` and drains the queue when the DI container disposes it — `IAsyncDisposable` is preferred so shutdown doesn't block a thread waiting on the drain).

**Level filtering**: `Enqueue` checks `IsEnabled(logLevel)` before formatting or queueing anything, so a disabled level (e.g. `Debug` when `Logging:LogLevel:Default` is `Information`) costs nothing — no string interpolation, no allocation, no channel write.

**Does not double-print to console**: this class calls `Console.WriteLine` directly and does **not** also route through the standard `ILogger<T>` pipeline — no service in this codebase configures a logging exporter (`ObservabilityExtensions.cs` wires up OpenTelemetry tracing/metrics only, not `.WithLogging()`), and none clears or reconfigures the default provider set, so ASP.NET Core's implicit default Console provider is still active for every category. An earlier version of this class also called the standard `_logger.Log(...)`, which printed every line twice. If a real `ILogger`-based sink is added later (an OTel logging exporter, for example), reintroduce that call on the background thread only — never on the caller's thread — and either silence the default Console provider for these categories or accept the double output deliberately.
