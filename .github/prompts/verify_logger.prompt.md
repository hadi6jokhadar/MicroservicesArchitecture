---
agent: "agent"
description: "Verify if an existing file is using ILogger correctly based on the application logging standards."
---

# Verify Logger Usage

To verify that a file (e.g., a Handler or Service) is using `ILogger<>` correctly, follow these checking steps:

## 1. Check the Constructor Injection

- Ensure `Microsoft.Extensions.Logging` is imported.
- Check that the logger is injected as `ILogger<ClassName>`, where `ClassName` is exactly the name of the class.
- Verify that the logger instance is assigned to a `private readonly ILogger<ClassName> _logger;` field.

## 2. Verify Structured Logging

- Scan the file for any calls to `_logger.LogInformation`, `_logger.LogWarning`, `_logger.LogError`, etc.
- Ensure that **none** of the log messages use string interpolation (`$"..."` or string concatenation `+`) for dynamic values.
- Confirm they use the template format: `_logger.LogInformation("Processing {ItemId}", item.Id);`.

## 3. Check Exception Blocks

- If there is a `try/catch` block, look at `catch (Exception ex)`.
- Verify that `_logger.LogError(ex, "Some appropriate message");` is called.
- Verify that `catch (AppException)` does **not** redundantly log the error, and just executes `throw;` or is intentionally left without logging.

## 4. Actionable Outcome

- If any violations are found, fix them directly by modifying the source code.
- If the file is compliant, summarize that the logger usage has been verified and meets the project standards.
