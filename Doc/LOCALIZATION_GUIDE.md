# 🌍 Localization System Guide

**Complete Multi-Language Support for Microservices Architecture**

**Last Updated:** May 2026  
**Version:** 1.1  
**Status:** ✅ Production Ready

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

### **Components**

```
Localization System
├── ILocalizationService          # Core interface for translation
├── LocalizationService           # JSON file-based implementation
├── LocalizationKeys              # Strongly-typed key constants
├── LocalizationMiddleware        # Culture detection from headers
├── GlobalExceptionHandlingMiddleware  # Localized error responses
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

3. GlobalExceptionHandlingMiddleware
   ├─ Catches AppException
   ├─ Translates LocalizationKey using ILocalizationService
   └─ Returns localized error response

4. Response → Client (in requested language)
```

---

## 🚀 Quick Start

### **Step 1: Register Services**

In your service's `Program.cs`:

```csharp
using IhsanDev.Shared.Application.Extensions;
using IhsanDev.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ✅ Add localization services
builder.Services.AddLocalization();

var app = builder.Build();

// ✅ Use middlewares (ORDER MATTERS!)
app.UseGlobalExceptionHandling();  // First: catch all exceptions
app.UseRouting();                   // Second: routing
app.UseLocalization();              // Third: detect language
app.UseAuthentication();            // Fourth: authentication
app.UseAuthorization();             // Fifth: authorization

app.MapControllers();
app.Run();
```

### **Step 2: Copy Resource Files**

Ensure JSON files are copied to output directory. Add to `.csproj`:

```xml
<ItemGroup>
  <None Update="Resources\Localization\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Or reference from Shared.Application (already configured):

```xml
<ItemGroup>
  <Content Include="..\..\Shared\IhsanDev.Shared.Application\Resources\Localization\*.json">
    <Link>Resources\Localization\%(Filename)%(Extension)</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### **Step 3: Use in Your Code**

#### **Throwing Localized Exceptions**

```csharp
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;

// ✅ Option 1: Use localization key only (middleware will translate)
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

// 1. Register localization FIRST (before validators)
builder.Services.AddLocalization();

// 2. Register other services
builder.Services.AddControllers();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// 3. Register validators (they can now inject ILocalizationService)
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

var app = builder.Build();

// ✅ CORRECT MIDDLEWARE ORDER
app.UseGlobalExceptionHandling();  // 1st: Catch exceptions
app.UseRouting();                   // 2nd: Routing
app.UseLocalization();              // 3rd: Detect language
app.UseAuthentication();            // 4th: Auth
app.UseAuthorization();             // 5th: Authz
app.MapControllers();
app.Run();
```

### **Testing Language Detection**

```bash
# Test English (default)
curl https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"invalid","password":"test"}'

# Response: {"message": "Invalid email or password"}

# Test Arabic
curl https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -H "Accept-Language: ar" \
  -d '{"email":"invalid","password":"test"}'

# Response: {"message": "البريد الإلكتروني أو كلمة المرور غير صحيحة"}

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

# Response: {"message": "Utilisateur introuvable"}
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
// ❌ WRONG — GlobalExceptionHandlingMiddleware will NOT catch this
public class FileValidationException : Exception
{
    public FileValidationException(string message) : base(message) { }
}

throw new FileValidationException("File is empty.");
// Result: 500 Internal Server Error (unhelpful, not localized)
```

✅ **DO** use the `AppException` hierarchy with `LocalizationKeys`:

```csharp
// ✅ CORRECT — GlobalExceptionHandlingMiddleware handles this properly
throw new BadRequestException(LocalizationKeys.Exceptions.FileEmpty);
// Result: 400 Bad Request with localized message
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

1. Check JSON files exist in `bin/Debug/net8.0/Resources/Localization/`
2. Verify `.csproj` has `CopyToOutputDirectory` configuration
3. Check file names: `en.json`, `ar.json` (lowercase)

```xml
<ItemGroup>
  <Content Include="Resources\Localization\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### **Issue: Language Not Detected**

**Symptoms:** Always returns English regardless of header

**Solution:**

1. Check middleware registration: `app.UseLocalization()` **after** `app.UseRouting()`
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
// Without localization service (key returned as-is, middleware translates)
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

| Category      | Keys   | English | Arabic | Status   |
| ------------- | ------ | ------- | ------ | -------- |
| Exceptions    | 22     | ✅      | ✅     | Complete |
| Validation    | 12     | ✅      | ✅     | Complete |
| Success       | 9      | ✅      | ✅     | Complete |
| Common UI     | 14     | ✅      | ✅     | Complete |
| Notifications | 6      | ✅      | ✅     | Complete |
| OTP           | 6      | ✅      | ✅     | Complete |
| **Total**     | **69** | **✅**  | **✅** | **100%** |

### **Exceptions — Key Reference**

| Key Constant                   | JSON Key                                     | Added In |
| ------------------------------ | -------------------------------------------- | -------- |
| `SongNotFound`                 | `exception_song_not_found`                   | v1.1     |
| `ArtistNotFound`               | `exception_artist_not_found`                 | v1.1     |
| `IngestionJobNotFound`         | `exception_ingestion_job_not_found`          | v1.1     |
| `SongArtistChangeNotSupported` | `exception_song_artist_change_not_supported` | v1.1     |
| `SongNotIndexed`               | `exception_song_not_indexed`                 | v1.1     |
| `TokenTenantHeaderMissing`     | `exception_token_tenant_header_missing`      | v1.1     |
| `TokenTenantMismatch`          | `exception_token_tenant_mismatch`            | v1.1     |

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
  "statusCode": 401,
  "title": "Unauthorized access",
  "message": "Invalid email or password",
  "localizationKey": "exception_invalid_credentials",
  "traceId": "0HN7GLLMTQ8K1:00000001",
  "timestamp": "2025-11-17T10:30:00Z"
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
  "statusCode": 401,
  "title": "وصول غير مصرح به",
  "message": "البريد الإلكتروني أو كلمة المرور غير صحيحة",
  "localizationKey": "exception_invalid_credentials",
  "traceId": "0HN7GLLMTQ8K1:00000002",
  "timestamp": "2025-11-17T10:30:00Z"
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
