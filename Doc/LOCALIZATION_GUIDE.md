# 🌍 Localization System Guide

**Complete Multi-Language Support for Microservices Architecture**

**Last Updated:** August 2026  
**Version:** 1.2  
**Status:** ✅ Production Ready

> **v1.2 correction:** This doc previously described `GlobalExceptionHandlingMiddleware` (custom `{statusCode, title, message, localizationKey, traceId, timestamp}` JSON) as the live exception pipeline, `builder.Services.AddLocalization()` as the registration call, a "69 keys / 6 categories" coverage table, and per-service `.csproj` snippets for copying resource files. All four were stale/incorrect — verified against current source and corrected throughout this document. The real pipeline is `GlobalExceptionHandler` (`IExceptionHandler` + RFC 7807 `ProblemDetails`), registered via `AddGlobalExceptionHandler()`/`app.UseGlobalExceptionHandler()`; localization is registered via `AddLocalizationService()`; the coverage table now reflects 190 keys across 10 categories; and resource-file copying is automatic via project-reference propagation from `IhsanDev.Shared.Application`, with no per-service `.csproj` entry needed.

---

## 📋 Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Usage Patterns](#usage-patterns)
- [Integration Guide](#integration-guide)
- [Adding New Languages](#adding-new-languages)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)
- [API Reference](#api-reference)

---

## 🎯 Overview

The localization system provides comprehensive multi-language support across all microservices with:

✅ **JSON-Based Translations**: Easy-to-maintain JSON resource files  
✅ **Automatic Culture Detection**: From `Accept-Language` or custom `x-culture` header  
✅ **Exception Localization**: All exceptions return translated messages  
✅ **Validation Localization**: FluentValidation messages in user's language  
✅ **Type-Safe Keys**: Strongly-typed localization keys via `LocalizationKeys`  
✅ **Caching**: In-memory caching for performance (24-hour TTL)  
✅ **Fallback Support**: Automatic fallback to English if translation missing  
✅ **Multiple Cultures**: Currently supports English (en) and Arabic (ar)

---

## 🏗️ Architecture

> ⚠️ **Doc correction (August 2026):** This section previously described a `GlobalExceptionHandlingMiddleware` class producing a custom `{statusCode, title, message, localizationKey, traceId, timestamp}` JSON body. That class still exists in the codebase (`IhsanDev.Shared.Infrastructure/Middleware/GlobalExceptionHandlingMiddleware.cs`) but it is **dead code** — no real service registers it. Every real service (Identity, Category, FileManager, Notification, Tenant, Translation, Backup, and the Nasheed/PolySnap apps) uses **`GlobalExceptionHandler`**, an ASP.NET Core `IExceptionHandler` registered via `AddGlobalExceptionHandler()`/`app.UseGlobalExceptionHandler()`, which produces an **RFC 7807 `ProblemDetails`** response. The sections below describe the real pipeline.

### **Components**

```
Localization System
├── ILocalizationService          # Core interface for translation
├── LocalizationService           # JSON file-based implementation
├── LocalizationKeys              # Strongly-typed key constants
├── LocalizationMiddleware        # Culture detection from headers
├── GlobalExceptionHandler        # IExceptionHandler — localized RFC 7807 ProblemDetails responses
├── LocalizedValidationExtensions # FluentValidation helpers
└── Resources/Localization/       # JSON translation files
    ├── en.json                   # English translations
    └── ar.json                   # Arabic translations
```

### **Request Flow**

```
1. HTTP Request → LocalizationMiddleware
   ├─ Reads Accept-Language or x-culture header
   ├─ Sets CultureInfo.CurrentCulture
   └─ Updates ILocalizationService

2. Handler Execution
   ├─ Exceptions use LocalizationKeys
   ├─ Validators use LocalizedValidator<T>
   └─ Services access ILocalizationService

3. GlobalExceptionHandler (ASP.NET Core IExceptionHandler, registered via
   AddGlobalExceptionHandler() + app.UseGlobalExceptionHandler())
   ├─ Catches AppException (and FluentValidation's ValidationException,
   │  UnauthorizedAccessException, KeyNotFoundException, InvalidOperationException,
   │  plus a catch-all for anything else)
   ├─ Translates AppException.Title / AppException.LocalizationKey using ILocalizationService
   └─ Writes an RFC 7807 ProblemDetails response (application/problem+json)

4. Response → Client (in requested language)
```

`app.UseGlobalExceptionHandler()` must be the **first** middleware registered in the pipeline (see `Doc/SERVICE_STARTUP_SEQUENCES.md`) so it wraps every other middleware, including `UseCorrelationId()`/`UseLocalization()` themselves.

---

## 🚀 Quick Start

### **Step 1: Register Services**

In your service's `Program.cs` (this is the real, current pattern — see Identity/Category/FileManager/Notification/Tenant/Translation/Backup `Program.cs` for live examples):

```csharp
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Extensions;   // AddGlobalExceptionHandler, AddLocalizationService, UseGlobalExceptionHandler, UseLocalization, UseCorrelationId

var builder = WebApplication.CreateBuilder(args);

// ✅ Register the exception handler (IExceptionHandler) + ASP.NET Core's built-in ProblemDetails service
builder.Services.AddGlobalExceptionHandler();

// ✅ Add localization services (reads Resources/Localization/*.json from AppDomain.CurrentDomain.BaseDirectory)
builder.Services.AddLocalizationService();

var app = builder.Build();

// ✅ Middleware order — GlobalExceptionHandler MUST be first so it wraps
// every other middleware, including UseCorrelationId()/UseLocalization() themselves.
// See Dotnet.instructions.md pitfall #42 and Doc/SERVICE_STARTUP_SEQUENCES.md.
app.UseGlobalExceptionHandler();    // First: catch all exceptions → RFC 7807 ProblemDetails
app.UseCorrelationId();             // Second: attach correlation/trace id
app.UseLocalization();              // Third: detect language (Accept-Language / x-culture)
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); // Minimal APIs: use app.MapGet/MapPost/... instead — see Dotnet.instructions.md
app.Run();
```

### **Step 2: Resource Files Are Copied Automatically — No Per-Service `.csproj` Entry Needed**

The `en.json`/`ar.json` resource files live in `src/Shared/IhsanDev.Shared.Application/Resources/Localization/` and are declared there with:

```xml
<!-- IhsanDev.Shared.Application.csproj -->
<ItemGroup>
  <None Update="Resources\Localization\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Because every service already has a `ProjectReference` chain that reaches `IhsanDev.Shared.Application` (directly or transitively via `.Infrastructure`), MSBuild's SDK-style project-reference propagation automatically flows these `None`/`CopyToOutputDirectory` items into **every** referencing project's own output directory — `{Service}.API/bin/{Config}/{TargetFramework}/Resources/Localization/{en,ar}.json`. This is why `LocalizationService`'s default resource path (`AppDomain.CurrentDomain.BaseDirectory + "Resources/Localization"`, resolved per-service by `AddLocalizationService()` in `InfrastructureServiceExtensions.cs`) finds the files without any extra configuration.

**No real service `.csproj` (e.g. `Identity.API.csproj`) contains a `<None Update="Resources\Localization\*.json">` or `<Content Include="...">` block of its own** — do not add one. If you add a brand-new key, only edit the JSON files in `IhsanDev.Shared.Application/Resources/Localization/`; every service picks it up automatically on its next build.

### **Step 3: Use in Your Code**

#### **Throwing Localized Exceptions**

```csharp
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;

// ✅ Option 1: Use localization key only (GlobalExceptionHandler will translate it)
throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

// ✅ Option 2: Inject ILocalizationService and translate immediately
public class MyService
{
    private readonly ILocalizationService _localization;

    public MyService(ILocalizationService localization)
    {
        _localization = localization;
    }

    public void DoSomething()
    {
        throw new NotFoundException(
            LocalizationKeys.Exceptions.UserNotFound,
            _localization);
    }
}

// ✅ Option 3: With format arguments
throw new BadRequestException(
    LocalizationKeys.Validation.MaxLength,
    _localization,
    "Email",
    255);
```

#### **Localized Validators**

```csharp
using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;

public class RegisterUserCommandValidator : LocalizedValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator(ILocalizationService localizationService)
        : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "Email"))
            .EmailAddress()
            .WithMessage(L(LocalizationKeys.Validation.EmailInvalid));

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "Password"))
            .MinimumLength(8)
            .WithMessage(L(LocalizationKeys.Validation.PasswordTooShort, 8));
    }
}
```

#### **Getting Translations in Services**

```csharp
public class NotificationService
{
    private readonly ILocalizationService _localization;

    public NotificationService(ILocalizationService localization)
    {
        _localization = localization;
    }

    public async Task SendWelcomeNotification(int userId)
    {
        var title = _localization.GetString(LocalizationKeys.Notifications.WelcomeTitle);
        var message = _localization.GetString(LocalizationKeys.Notifications.WelcomeMessage);

        await SendNotificationAsync(userId, title, message);
    }
}
```

---

## 📘 Usage Patterns

### **Pattern 1: Exception Handling**

**Before (Hardcoded):**

```csharp
throw new NotFoundException("User not found");
```

**After (Localized):**

```csharp
throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);
```

### **Pattern 2: Validation Messages**

**Before (Hardcoded):**

```csharp
RuleFor(x => x.Email)
    .NotEmpty()
    .WithMessage("Email is required")
    .EmailAddress()
    .WithMessage("Invalid email address");
```

**After (Localized):**

```csharp
RuleFor(x => x.Email)
    .NotEmpty()
    .WithMessage(L(LocalizationKeys.Validation.Required, "Email"))
    .EmailAddress()
    .WithMessage(L(LocalizationKeys.Validation.EmailInvalid));
```

### **Pattern 3: Custom Messages**

**Before (Hardcoded):**

```csharp
return Results.Ok(new { message = "Profile updated successfully" });
```

**After (Localized):**

```csharp
return Results.Ok(new
{
    message = _localization.GetString(LocalizationKeys.Success.ProfileUpdated)
});
```

### **Pattern 4: Format Arguments**

```csharp
// Localization key in en.json: "otp_resend_cooldown": "Please wait {0} seconds"
// Localization key in ar.json: "otp_resend_cooldown": "يرجى الانتظار {0} ثانية"

var message = _localization.GetString(
    LocalizationKeys.Otp.ResendCooldown,
    60);

// Result (en): "Please wait 60 seconds"
// Result (ar): "يرجى الانتظار 60 ثانية"
```

---

## 🔧 Integration Guide

### **Service Registration Order**

```csharp
// ✅ CORRECT ORDER in Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. Register MediatR + validators
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
builder.Services.AddValidatorsFromAssembly(applicationAssembly);

// 2. Register the exception handler
builder.Services.AddGlobalExceptionHandler();

// 3. Register localization (validators/handlers can inject ILocalizationService)
builder.Services.AddLocalizationService();

var app = builder.Build();

// ✅ CORRECT MIDDLEWARE ORDER — GlobalExceptionHandler MUST be first
app.UseGlobalExceptionHandler();    // 1st: catch exceptions → RFC 7807 ProblemDetails
app.UseCorrelationId();             // 2nd: correlation/trace id
app.UseLocalization();              // 3rd: detect language
app.UseRouting();
app.UseAuthentication();            // 4th: Auth
app.UseAuthorization();             // 5th: Authz
app.MapControllers(); // Minimal APIs in practice — see Dotnet.instructions.md
app.Run();
```

### **Testing Language Detection**

```bash
# Test English (default)
curl https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"invalid","password":"test"}'

# Response (ProblemDetails, see "Example 3: Error Response Format"): {"detail": "Invalid email or password", ...}

# Test Arabic
curl https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -H "Accept-Language: ar" \
  -d '{"email":"invalid","password":"test"}'

# Response: {"detail": "البريد الإلكتروني أو كلمة المرور غير صحيحة", ...}

# Test with custom header (overrides Accept-Language)
curl https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -H "x-culture: ar" \
  -d '{"email":"invalid","password":"test"}'
```

---

## 🌐 Adding New Languages

### **Step 1: Create JSON File**

Create `fr.json` (French):

```json
{
  "exception_bad_request": "Mauvaise demande",
  "exception_unauthorized": "Accès non autorisé",
  "exception_user_not_found": "Utilisateur introuvable",
  "validation_required": "{0} est requis",
  "validation_email_invalid": "Adresse e-mail invalide"
}
```

### **Step 2: Update Supported Cultures**

In `LocalizationMiddleware.cs`:

```csharp
private static readonly string[] SupportedCultures = { "en", "ar", "fr" };
```

### **Step 3: Test**

```bash
curl https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -H "Accept-Language: fr" \
  -d '{"email":"invalid","password":"test"}'

# Response: {"detail": "Utilisateur introuvable", ...}
```

---

## ✅ Best Practices

### **1. Always Use LocalizationKeys**

❌ **DON'T:**

```csharp
throw new NotFoundException("exception_user_not_found"); // Magic string
```

✅ **DO:**

```csharp
throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);
```

### **2. Inherit from LocalizedValidator**

❌ **DON'T:**

```csharp
public class MyValidator : AbstractValidator<MyCommand>
{
    public MyValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required");
    }
}
```

✅ **DO:**

```csharp
public class MyValidator : LocalizedValidator<MyCommand>
{
    public MyValidator(ILocalizationService localizationService)
        : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "Email"));
    }
}
```

### **3. Use Consistent Key Naming**

✅ **Follow Convention:**

- Exceptions: `exception_{category}_{name}` (e.g., `exception_user_not_found`)
- Validation: `validation_{rule}` (e.g., `validation_email_invalid`)
- Success: `success_{action}` (e.g., `success_registration_successful`)
- Common: `common_{term}` (e.g., `common_save`)
- Notifications: `notification_{event}_{type}` (e.g., `notification_welcome_title`)

### **4. Add New Keys to Both Files**

When adding a new key, **ALWAYS** update:

- `en.json` (English)
- `ar.json` (Arabic)
- Any other language files

### **5. Domain Exceptions Must Inherit from AppException**

❌ **DON'T** create custom exception classes that extend `Exception` directly:

```csharp
// ❌ WRONG — GlobalExceptionHandler's switch expression falls through to the
// catch-all (500) branch for any Exception subtype it doesn't recognize
public class FileValidationException : Exception
{
    public FileValidationException(string message) : base(message) { }
}

throw new FileValidationException("File is empty.");
// Result: 500 Internal Server Error (unhelpful, not localized)
```

✅ **DO** use the `AppException` hierarchy with `LocalizationKeys`:

```csharp
// ✅ CORRECT — GlobalExceptionHandler's AppException branch handles this
// (localizes Title + Detail, sets Status from appException.StatusCode)
throw new BadRequestException(LocalizationKeys.Exceptions.FileEmpty);
// Result: 400 Bad Request ProblemDetails with localized message
```

**AppException subclasses available:**

| Class                   | HTTP Status | Use For                              |
| ----------------------- | ----------- | ------------------------------------ |
| `BadRequestException`   | 400         | Invalid input, failed validation     |
| `UnauthorizedException` | 401         | Not authenticated                    |
| `ForbiddenException`    | 403         | Authenticated but not authorized     |
| `NotFoundException`     | 404         | Resource not found                   |
| `ConflictException`     | 409         | Duplicate resource or state conflict |
| `GeneralException`      | 500         | Unexpected internal errors           |

### **6. Handle Missing Translations Gracefully**

The system automatically falls back to English if translation is missing, but log warnings:

```csharp
// LocalizationService automatically logs warnings:
// "Translation key 'my_new_key' not found in culture 'ar', using default culture 'en'"
```

---

## 🐛 Troubleshooting

### **Issue: Translations Not Loading**

**Symptoms:** All responses return localization keys instead of translated text

**Solution:**

1. Check JSON files exist in `{Service}.API/bin/Debug/net10.0/Resources/Localization/` (they should appear automatically — see "Resource Files Are Copied Automatically" in Quick Start above)
2. Verify `src/Shared/IhsanDev.Shared.Application/IhsanDev.Shared.Application.csproj` still has its `<None Update="Resources\Localization\*.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>` block — this is the single source that propagates to every service via project-reference chaining
3. Check file names: `en.json`, `ar.json` (lowercase)
4. Confirm the service actually builds `IhsanDev.Shared.Application` into its dependency graph (directly or via `.Infrastructure`) — do **not** add a per-service `<None Update=...>`/`<Content Include=...>` block; the files should never need to be declared a second time in a service's own `.csproj`

### **Issue: Language Not Detected**

**Symptoms:** Always returns English regardless of header

**Solution:**

1. Check middleware registration: `app.UseLocalization()` must run after `app.UseGlobalExceptionHandler()`/`app.UseCorrelationId()` (so it's still covered by the exception handler) — see the real pipeline order in Quick Start above
2. Verify header name: `Accept-Language: ar` or `x-culture: ar`
3. Check supported cultures in `LocalizationMiddleware.cs`

### **Issue: Validation Messages Not Localized**

**Symptoms:** Exceptions are localized but validation errors are not

**Solution:**

1. Validator must inherit from `LocalizedValidator<T>`
2. Inject `ILocalizationService` in constructor
3. Use `L()` method or `LocalizationKeys`

> ⚠️ **Common pitfall:** Query validators (e.g. `GetSongListQueryValidator`) are often written as `AbstractValidator<T>` with hardcoded strings because they seem "simple." They must still use `LocalizedValidator<T>`. There is no exception for query validators.

### **Issue: Cache Not Clearing**

**Symptoms:** Translations not updating after JSON file changes

**Solution:**

1. Restart application (cache TTL is 24 hours)
2. Or clear cache programmatically:

```csharp
_memoryCache.Remove($"Localization_{culture}");
```

---

## 📚 API Reference

### **ILocalizationService**

```csharp
public interface ILocalizationService
{
    /// <summary>
    /// Get localized string by key
    /// </summary>
    string GetString(string key);

    /// <summary>
    /// Get localized string with format arguments
    /// </summary>
    string GetString(string key, params object[] args);

    /// <summary>
    /// Get current culture code (e.g., "en", "ar")
    /// </summary>
    string GetCurrentCulture();

    /// <summary>
    /// Set current culture
    /// </summary>
    void SetCulture(string culture);

    /// <summary>
    /// Check if key exists
    /// </summary>
    bool HasKey(string key);
}
```

### **LocalizationKeys Class**

```csharp
public static class LocalizationKeys
{
    public static class Exceptions
    {
        public const string BadRequest = "exception_bad_request";
        public const string UserNotFound = "exception_user_not_found";
        // ... more keys
    }

    public static class Validation
    {
        public const string Required = "validation_required";
        public const string EmailInvalid = "validation_email_invalid";
        // ... more keys
    }

    public static class Success
    {
        public const string RegistrationSuccessful = "success_registration_successful";
        // ... more keys
    }
}
```

### **AppException Constructors**

```csharp
// Without localization service (key returned as-is, GlobalExceptionHandler translates it)
throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

// With localization service (immediate translation)
throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound, _localization);

// With format arguments
throw new BadRequestException(
    LocalizationKeys.Validation.MaxLength,
    _localization,
    "Email",
    255);
```

---

## 📊 Localization Coverage

> ⚠️ **Recounted August 2026** directly from `src/Shared/IhsanDev.Shared.Application/Localization/LocalizationKeys.cs` — the table below reflects the actual current key count. It has grown substantially since the "69 keys / 6 categories" figure this doc previously quoted, and now has 10 categories, not 6.

| Category      | Keys    | English | Arabic | Status   |
| ------------- | ------- | ------- | ------ | -------- |
| Exceptions    | 53      | ✅      | ✅     | Complete |
| Validation    | 34      | ✅      | ✅     | Complete |
| Fields        | 49      | ✅      | ✅     | Complete |
| Success       | 16      | ✅      | ✅     | Complete |
| Common UI     | 15      | ✅      | ✅     | Complete |
| Notifications | 6       | ✅      | ✅     | Complete |
| OTP           | 8       | ✅      | ✅     | Complete |
| Error         | 2       | ✅      | ✅     | Complete |
| Tenant        | 6       | ✅      | ✅     | Complete |
| CORS          | 1       | ✅      | ✅     | Complete |
| **Total**     | **190** | **✅**  | **✅** | **100%** |

### **Exceptions — Key Reference (selected additions)**

| Key Constant                   | JSON Key                                     | Category                |
| ------------------------------ | --------------------------------------------- | ------------------------ |
| `SongNotFound`                 | `exception_song_not_found`                    | Nasheed-specific          |
| `ArtistNotFound`               | `exception_artist_not_found`                  | Nasheed-specific          |
| `IngestionJobNotFound`         | `exception_ingestion_job_not_found`           | Nasheed-specific          |
| `SongArtistChangeNotSupported` | `exception_song_artist_change_not_supported`  | Nasheed-specific          |
| `SongNotIndexed`               | `exception_song_not_indexed`                  | Nasheed-specific          |
| `TokenTenantHeaderMissing`     | `exception_token_tenant_header_missing`       | JWT tenant verification   |
| `TokenTenantMismatch`          | `exception_token_tenant_mismatch`             | JWT tenant verification   |
| `AuditLogNotFound`             | `exception_audit_log_not_found`               | Audit                    |
| `FeatureNotEnabled`            | `exception_feature_not_enabled`               | Feature flags             |
| `BackupTargetNotFound`         | `exception_backup_target_not_found`           | Backup service            |
| `BackupRunNotFound`            | `exception_backup_run_not_found`              | Backup service            |
| `BackupToolNotFound`           | `exception_backup_tool_not_found`             | Backup service            |
| `BackupProcessFailedWithDetails` | `exception_backup_process_failed_with_details` | Backup service          |

`LocalizationKeys.cs` also has a `Fields` category (49 keys) not previously documented — field-name keys (e.g. `field_email`, `field_tenant_id`) used as format arguments in validation messages like `L(LocalizationKeys.Validation.Required, LocalizationKeys.Fields.Email)`.

---

## 🔄 Migration from Hardcoded Strings

### **Step-by-Step Migration**

1. **Find Hardcoded Strings**

   ```bash
   # Search for exception messages
   grep -r "throw new.*Exception(\"" src/
   ```

2. **Add Localization Keys**
   - Add new keys to `LocalizationKeys.cs`
   - Add translations to `en.json` and `ar.json`

3. **Update Code**

   ```csharp
   // Before
   throw new NotFoundException("User not found");

   // After
   throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);
   ```

4. **Test Both Languages**

   ```bash
   # English
   curl -H "Accept-Language: en" https://localhost:5001/api/...

   # Arabic
   curl -H "Accept-Language: ar" https://localhost:5001/api/...
   ```

---

## 🎓 Examples

### **Example 1: Login Handler with Localization**

```csharp
public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDtoIncludesToken>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILocalizationService _localization;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        ILocalizationService localization)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _localization = localization;
    }

    public async Task<UserDtoIncludesToken> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            throw new UnauthorizedException(
                LocalizationKeys.Exceptions.InvalidCredentials,
                _localization);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException(
                LocalizationKeys.Exceptions.InvalidCredentials,
                _localization);
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        return UserDtoIncludesToken.MapFrom(user, token);
    }
}
```

### **Example 2: Register Validator with Localization**

```csharp
public class RegisterUserCommandValidator : LocalizedValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator(ILocalizationService localizationService)
        : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "Email"))
            .EmailAddress()
            .WithMessage(L(LocalizationKeys.Validation.EmailInvalid))
            .MaximumLength(255)
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, "Email", 255));

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "Password"))
            .MinimumLength(8)
            .WithMessage(L(LocalizationKeys.Validation.PasswordTooShort, 8))
            .Matches(@"[0-9]")
            .WithMessage(L(LocalizationKeys.Validation.PasswordRequiresDigit))
            .Matches(@"[A-Z]")
            .WithMessage(L(LocalizationKeys.Validation.PasswordRequiresUppercase));
    }
}
```

### **Example 3: Error Response Format**

> ⚠️ **Corrected August 2026.** The real response shape is RFC 7807 `ProblemDetails` (`Content-Type: application/problem+json`), produced by `GlobalExceptionHandler.CreateProblemDetails` (`IhsanDev.Shared.Infrastructure/Middleware/GlobalExceptionHandler.cs`) — **not** the `{statusCode, title, message, localizationKey, timestamp}` shape previously shown here (that shape belongs to the unused `GlobalExceptionHandlingMiddleware` class). Fields:
>
> | Field                 | Source                                                                    |
> | ---------------------- | -------------------------------------------------------------------------- |
> | `status`               | `AppException.StatusCode` (e.g. 401)                                      |
> | `title`                | `ILocalizationService.GetString(AppException.Title)` — a localized short summary |
> | `detail`               | `ILocalizationService.GetString(AppException.Message)` — the localized message |
> | `instance`             | `httpContext.Request.Path`                                                |
> | `type`                 | Standard `ProblemDetails.Type` (RFC 7807 URI; omitted/default unless set) |
> | `extensions.traceId`   | `httpContext.TraceIdentifier`                                             |
> | `extensions.errors`    | Only present for FluentValidation failures — `{ propertyName(camelCase): string[] }` |

**Request (English):**

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -H "Accept-Language: en" \
  -d '{"email":"invalid","password":"test"}'
```

**Response (English):**

```json
{
  "status": 401,
  "title": "Unauthorized access",
  "detail": "Invalid email or password",
  "instance": "/api/v1/auth/login",
  "traceId": "0HN7GLLMTQ8K1:00000001"
}
```

**Request (Arabic):**

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -H "Accept-Language: ar" \
  -d '{"email":"invalid","password":"test"}'
```

**Response (Arabic):**

```json
{
  "status": 401,
  "title": "وصول غير مصرح به",
  "detail": "البريد الإلكتروني أو كلمة المرور غير صحيحة",
  "instance": "/api/v1/auth/login",
  "traceId": "0HN7GLLMTQ8K1:00000002"
}
```

**Validation failure example (FluentValidation → 400 with an `errors` dictionary):**

```json
{
  "status": 400,
  "title": "Bad request",
  "detail": "One or more validation errors occurred",
  "instance": "/api/v1/auth/register",
  "traceId": "0HN7GLLMTQ8K1:00000003",
  "errors": {
    "email": ["Email is required"],
    "password": ["Password must be at least 8 characters"]
  }
}
```

---

## 🚀 Performance Considerations

### **Caching**

- Translations cached in memory for **24 hours**
- Cache key: `Localization_{culture}` (e.g., `Localization_en`)
- Cache cleared on application restart

### **Memory Usage**

- Each language file: ~5-10 KB
- Cached in memory: ~10-20 KB per language
- Minimal impact on overall memory

### **Load Time**

- First request per language: ~5-10 ms (file read + parse)
- Subsequent requests: <1 ms (cache hit)

---

## 📝 Summary

✅ **Implemented:**

- JSON-based localization system
- Culture detection from headers
- Exception localization
- Validation localization
- Type-safe localization keys
- Caching and fallback support
- Middleware integration
- English and Arabic translations

✅ **Benefits:**

- User-friendly error messages in native language
- Consistent translation approach across all services
- Easy to add new languages
- No code changes required for new languages
- Performance-optimized with caching

✅ **Next Steps:**

- Add more languages (French, Spanish, etc.)
- Implement tenant-specific translations (override system defaults)
- Add localization for notification templates
- Create admin UI for managing translations

---

**Built with ❤️ for Multi-Language Support**

_For questions or issues, check the relevant guide or create a GitHub issue._
