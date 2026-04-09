---
agent: "agent"
description: "Guide on how to write and use ILogger in handlers or other classes in the application."
---

# How to Use ILogger<> in Handlers

When creating a new handler or service that requires logging, follow these rules for injecting and utilizing `ILogger<>`:

## 1. Dependency Injection

Inject `ILogger<T>` via the constructor, where `T` is the type of the current class.

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

Use structured logging rather than string interpolation when passing variables to the log message. This ensures variables can be indexed by log aggregators.

- ✅ **Good**: `_logger.LogWarning("User with ID {UserId} not found", request.UserId);`
- ❌ **Bad**: `_logger.LogWarning($"User with ID {request.UserId} not found");`

## 3. Log Levels

- Use `LogInformation` for normal operational flow (e.g., successful creation, state changes).
- Use `LogWarning` for expected error conditions or domain validations (e.g., entity not found).
- Use `LogError` for unexpected exceptions.

## 4. Exception Handling

- Always log exceptions in the general `catch (Exception ex)` block using `_logger.LogError(ex, "Failed to [action description]");`.
- Do **NOT** catch `AppException` just to log it. If you have a `catch (AppException)` block, simply rethrow it (`throw;`), as domain exceptions should be logged or handled globally.
