# Centralized Validation Error Handling - Quick Reference

## What Changed?

All microservices now use the **same localized validation error handling** instead of each service having its own implementation.

## How It Works

```
User sends invalid data → Endpoint Filter validates → Returns localized error response
```

## Error Response Format (All Services)

```json
{
  "status": 400,
  "title": "Bad Request",
  "detail": "One or more validation errors occurred",
  "instance": "/api/path",
  "traceId": "...",
  "errors": {
    "fieldName": ["error message 1", "error message 2"]
  }
}
```

## For API Consumers

- ✅ All services return consistent error structures
- ✅ Error messages are localized based on language settings
- ✅ Property-level error details help identify exact validation issues
- ✅ `traceId` helps with debugging and support

## For Developers

### Adding Validation to an Endpoint

```csharp
// 1. Create a command with validator
public record CreateUserCommand(string Email, string Password) : IRequest<UserDto>;

public class CreateUserCommandValidator : LocalizedValidator<CreateUserCommand>
{
    public CreateUserCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Email).EmailAddress();
        RuleFor(x => x.Password).MinimumLength(8);
    }
}

// 2. Map the endpoint with ValidationFilter
app.MapPost("/api/users", YourHandler)
    .Produces<UserDto>(200)
    .ProducesValidationProblem()
    .AddEndpointFilter<ValidationFilter<CreateUserCommand>>();  // ← This line
```

### Services Using This Pattern

| Service      | Filters Applied        | Details                                               |
| ------------ | ---------------------- | ----------------------------------------------------- |
| Identity     | All auth endpoints     | `/api/auth/register`, `/api/auth/login`, etc.         |
| Notification | Send & Queue endpoints | `/api/notifications/send`, `/api/notifications/queue` |
| Tenant       | Admin endpoints        | `/api/admin/tenant/` POST & PUT                       |
| FileManager  | Upload endpoints       | Uses file form validation                             |

## Key Locations

- **Shared Filter**: `src/Shared/IhsanDev.Shared.Infrastructure/Filters/ValidationFilter.cs` (base implementation)
- **Service Filters**: Each service has its own `Filters/ValidationFilter.cs` (thin wrapper)
- **Localization Keys**: Added to `LocalizationKeys.Exceptions` class
- **Translations**: `en.json` and `ar.json` in localization resources

## Localization Keys

| Key                          | English                                               | Arabic                                            |
| ---------------------------- | ----------------------------------------------------- | ------------------------------------------------- |
| `exception_validation_error` | One or more validation errors occurred                | حدث خطأ واحد أو أكثر من أخطاء التحقق              |
| `exception_unexpected_error` | An unexpected error occurred. Please try again later. | حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى لاحقاً. |

## Testing

```bash
# Test with invalid registration data
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"invalid"}'

# Response: 400 Bad Request with localized errors
```

## FAQ

**Q: Do I need to update my endpoint if I add a new command?**  
A: Yes, add `.AddEndpointFilter<ValidationFilter<YourCommand>>()` to the endpoint mapping.

**Q: What if my endpoint doesn't have a command?**  
A: You don't need the validation filter. The GlobalExceptionHandler will catch any unhandled exceptions.

**Q: Can I customize error messages?**  
A: Yes, your validators define the messages. Use `LocalizationService.GetString()` in validators for localized messages.

**Q: Which services are affected?**  
A: Identity, Notification, Tenant, and FileManager - all using the shared filter.

---

**Documentation Status**: ✅ Complete  
**Last Updated**: January 15, 2026
