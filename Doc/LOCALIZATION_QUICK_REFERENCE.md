# 🌍 Localization Quick Reference

**One-Page Cheat Sheet for Multi-Language Support**

---

## 🚀 Quick Setup (3 Steps)

### **1. Register Services (Program.cs)**

```csharp
// Add services
builder.Services.AddLocalization();

// Add middlewares (ORDER MATTERS!)
app.UseGlobalExceptionHandling();  // 1st
app.UseRouting();                   // 2nd
app.UseLocalization();              // 3rd
app.UseAuthentication();            // 4th
```

### **2. Copy JSON Files**

Add to `.csproj`:

```xml
<ItemGroup>
  <Content Include="..\..\Shared\IhsanDev.Shared.Application\Resources\Localization\*.json">
    <Link>Resources\Localization\%(Filename)%(Extension)</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### **3. Use in Code**

```csharp
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Exceptions;

throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);
```

---

## 📘 Common Patterns

### **Exceptions**

```csharp
// Simple (middleware translates)
throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

// With service (immediate translation)
throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound, _localization);

// With format arguments
throw new BadRequestException(
    LocalizationKeys.Validation.MaxLength,
    _localization,
    "Email",
    255);
```

### **Validators**

```csharp
public class MyValidator : LocalizedValidator<MyCommand>
{
    public MyValidator(ILocalizationService localizationService)
        : base(localizationService)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "Email"))
            .EmailAddress()
            .WithMessage(L(LocalizationKeys.Validation.EmailInvalid));
    }
}
```

### **Custom Messages**

```csharp
public class MyService
{
    private readonly ILocalizationService _localization;

    public MyService(ILocalizationService localization)
    {
        _localization = localization;
    }

    public string GetWelcomeMessage()
    {
        return _localization.GetString(LocalizationKeys.Notifications.WelcomeMessage);
    }
}
```

---

## 🌐 Supported Languages

| Language | Code | Status      |
| -------- | ---- | ----------- |
| English  | en   | ✅ Default  |
| Arabic   | ar   | ✅ Complete |

---

## 🔑 Available Localization Keys

### **Exceptions**

```csharp
LocalizationKeys.Exceptions.BadRequest
LocalizationKeys.Exceptions.Unauthorized
LocalizationKeys.Exceptions.Forbidden
LocalizationKeys.Exceptions.NotFound
LocalizationKeys.Exceptions.Conflict
LocalizationKeys.Exceptions.InternalServerError
LocalizationKeys.Exceptions.UserNotFound
LocalizationKeys.Exceptions.InvalidCredentials
LocalizationKeys.Exceptions.EmailAlreadyExists
LocalizationKeys.Exceptions.InvalidToken
LocalizationKeys.Exceptions.FileNotFound
```

### **Validation**

```csharp
LocalizationKeys.Validation.Required
LocalizationKeys.Validation.EmailInvalid
LocalizationKeys.Validation.PasswordTooShort
LocalizationKeys.Validation.PasswordRequiresDigit
LocalizationKeys.Validation.PasswordRequiresUppercase
LocalizationKeys.Validation.MaxLength
LocalizationKeys.Validation.MinLength
LocalizationKeys.Validation.PhoneNumberInvalid
```

### **Success Messages**

```csharp
LocalizationKeys.Success.RegistrationSuccessful
LocalizationKeys.Success.LoginSuccessful
LocalizationKeys.Success.ProfileUpdated
LocalizationKeys.Success.FileUploaded
LocalizationKeys.Success.NotificationSent
```

### **OTP**

```csharp
LocalizationKeys.Otp.CodeSent
LocalizationKeys.Otp.CodeExpired
LocalizationKeys.Otp.CodeInvalid
LocalizationKeys.Otp.AccountLocked
LocalizationKeys.Otp.ResendCooldown
```

---

## 🧪 Testing

### **Test English (Default)**

```bash
curl https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"invalid","password":"test"}'
```

### **Test Arabic**

```bash
curl https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -H "Accept-Language: ar" \
  -d '{"email":"invalid","password":"test"}'
```

### **Test with Custom Header**

```bash
curl https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -H "x-culture: ar" \
  -d '{"email":"invalid","password":"test"}'
```

---

## 🆕 Adding New Keys

### **1. Add to LocalizationKeys.cs**

```csharp
public static class Exceptions
{
    public const string MyNewError = "exception_my_new_error";
}
```

### **2. Add to en.json**

```json
{
  "exception_my_new_error": "My new error message"
}
```

### **3. Add to ar.json**

```json
{
  "exception_my_new_error": "رسالة الخطأ الجديدة"
}
```

### **4. Use in Code**

```csharp
throw new BadRequestException(LocalizationKeys.Exceptions.MyNewError);
```

---

## 🆕 Adding New Language

### **1. Create fr.json**

```json
{
  "exception_bad_request": "Mauvaise demande",
  "exception_user_not_found": "Utilisateur introuvable"
}
```

### **2. Update LocalizationMiddleware.cs**

```csharp
private static readonly string[] SupportedCultures = { "en", "ar", "fr" };
```

### **3. Test**

```bash
curl -H "Accept-Language: fr" https://localhost:5001/api/...
```

---

## ⚡ Key Features

✅ **Automatic Detection**: Language from `Accept-Language` or `x-culture` header  
✅ **Type-Safe Keys**: Use `LocalizationKeys` constants  
✅ **Fallback Support**: Missing translations fall back to English  
✅ **Caching**: 24-hour in-memory cache for performance  
✅ **Format Arguments**: Support for `{0}`, `{1}` placeholders  
✅ **Global Exception Handling**: All exceptions automatically localized

---

## 🐛 Troubleshooting

| Issue                         | Solution                                                     |
| ----------------------------- | ------------------------------------------------------------ |
| Keys returned instead of text | Check JSON files copied to `bin/.../Resources/Localization/` |
| Always English                | Verify `UseLocalization()` middleware registered             |
| Validation not localized      | Inherit from `LocalizedValidator<T>`                         |
| Cache not updating            | Restart app (24-hour TTL)                                    |

---

## 📚 Full Documentation

For complete guide, see: `Doc/LOCALIZATION_GUIDE.md`

---

**Print this card and keep it handy! 📋**
