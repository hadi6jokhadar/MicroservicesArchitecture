---
name: be-use-logger
description: Reference guide for correctly injecting and using ILogger<T> in .NET handlers and services in this project — structured logging rules, log level selection, and exception handling patterns. Use this whenever the user asks how to add logging, use a logger, log messages, write log statements, or has any question about ILogger usage in a handler or service.
---

# How to Use ILogger<> in Handlers

## 1. Dependency Injection

Inject `ILogger<T>` via the constructor, where `T` is the type of the current class:

```csharp
using Microsoft.Extensions.Logging;

public class MyCommandHandler : IRequestHandler<MyCommand, bool>
{
    private readonly ILogger<MyCommandHandler> _logger;

    public MyCommandHandler(ILogger<MyCommandHandler> logger)
    {
        _logger = logger;
    }
}
```

## 2. Structured Logging

Use structured logging rather than string interpolation so variables can be indexed by log aggregators:

- **Good**: `_logger.LogWarning("User with ID {UserId} not found", request.UserId);`
- **Bad**: `_logger.LogWarning($"User with ID {request.UserId} not found");`

The `{PlaceholderName}` tokens become queryable properties in log aggregators (e.g. Seq, Grafana Loki). String interpolation destroys that structure.

## 3. Log Levels

| Level | When to use |
|---|---|
| `LogInformation` | Normal operational flow — successful creation, state changes |
| `LogWarning` | Expected error conditions or domain validations — entity not found, invalid state |
| `LogError` | Unexpected exceptions that indicate a bug or infrastructure failure |

## 4. Exception Handling

- Always log exceptions in the general `catch (Exception ex)` block:
  ```csharp
  _logger.LogError(ex, "Failed to create {Entity}", nameof(MyEntity));
  ```
- Do **NOT** catch `AppException` just to log it — domain exceptions are handled globally by `GlobalExceptionHandlingMiddleware`. If you have a `catch (AppException)` block, rethrow it with `throw;` without logging.
