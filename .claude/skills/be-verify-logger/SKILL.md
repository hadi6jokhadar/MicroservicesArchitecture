---
name: be-verify-logger
description: Audit an existing .NET handler or service for correct ILogger<T> usage — checks constructor injection, structured logging (no string interpolation), and exception handling. Use this whenever the user asks to check, verify, audit, review, or fix logging in a handler, service, or any .NET class. Also use it proactively when reviewing a handler that uses _logger.
---

# Verify Logger Usage

Use this workflow to audit an existing handler or service for correct `ILogger<>` usage.

## 1. Check the Constructor Injection

- Ensure `Microsoft.Extensions.Logging` is imported
- Logger is injected as `ILogger<ClassName>` where `ClassName` is exactly the name of the class
- Logger instance is assigned to `private readonly ILogger<ClassName> _logger;`

## 2. Verify Structured Logging

- Scan for calls to `_logger.LogInformation`, `_logger.LogWarning`, `_logger.LogError`, etc.
- Ensure **none** use string interpolation (`$"..."`) or concatenation (`+`) for dynamic values
- Confirm template format: `_logger.LogInformation("Processing {ItemId}", item.Id);`

## 3. Check Exception Blocks

- In `catch (Exception ex)`, verify `_logger.LogError(ex, "Failed to [description]");` is called
- Verify `catch (AppException)` does **not** redundantly log — it should just `throw;`

## 4. Actionable Outcome

- If violations found: fix them directly by modifying the source code
- If compliant: summarize that logger usage is verified and meets project standards
