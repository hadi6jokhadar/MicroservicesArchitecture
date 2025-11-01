# 📝 How to Add New Variables to TenantConfiguration

## Overview

This guide explains **step-by-step** how to add new properties to `TenantConfiguration`. The `TenantConfiguration` class contains tenant-specific settings like JWT, Database, CORS, and OTP configurations. When you need to add new tenant-specific settings, follow these steps carefully.

---

## 🎯 Quick Reference

**Files You'll Modify:**

1. `TenantConfiguration.cs` - Add new property
2. Test files - Update test data
3. Database (optional) - Migrate if data property changes
4. Documentation - Update relevant guides

**Data Flow:**

```
User sends JSON → ASP.NET Core deserializes → TenantConfiguration object
     ↓
Application layer uses TenantConfiguration object
     ↓
Handler serializes to JSON string → Saves to PostgreSQL Data column
     ↓
Response: PostgreSQL JSON string → Deserializes to TenantConfiguration → Returns as JSON
```

---

## 📋 Step-by-Step Guide

### Step 1: Add Property to TenantConfiguration Class

**Location:** `src/Shared/IhsanDev.Shared.Kernel/Dto/Tenant/TenantConfiguration.cs`

```csharp
public class TenantConfiguration
{
    public JwtSettings? Jwt { get; set; }
    public DatabaseSettings? Database { get; set; }
    public CorsSettings? Cors { get; set; }
    public OtpSettings? Otp { get; set; }

    // ✅ Add your new property here
    public EmailSettings? Email { get; set; }  // Example: New email configuration
}
```

**Property Naming Convention:**

- Use **PascalCase** for property names (e.g., `EmailSettings`, `SmsProvider`)
- JSON serialization uses **camelCase** automatically (e.g., `email`, `smsProvider`)
- This is configured via `JsonNamingPolicy = JsonNamingPolicy.CamelCase`

---

### Step 2: Create the Settings Class (If Needed)

If your new property requires a complex object (like `EmailSettings`), create a new class:

**Location:** `src/Shared/IhsanDev.Shared.Kernel/Dto/Tenant/EmailSettings.cs`

```csharp
namespace IhsanDev.Shared.Kernel.Dto.Tenant;

public class EmailSettings
{
    public string? Provider { get; set; }          // "SendGrid", "SMTP", "AmazonSES"
    public string? ApiKey { get; set; }            // Provider API key
    public string? FromEmail { get; set; }         // Default sender email
    public string? FromName { get; set; }          // Default sender name
    public SmtpSettings? Smtp { get; set; }        // SMTP-specific settings
}

public class SmtpSettings
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
```

**Best Practices:**

- Use **nullable types** (`string?`, `int?`) to allow partial configurations
- Provide **default values** where appropriate (e.g., `Port = 587`)
- Use **nested classes** for complex settings (e.g., `SmtpSettings` within `EmailSettings`)
- Add **XML comments** for documentation

---

### Step 3: Update Validation (Optional)

If your new property requires validation, update the validators:

**Location:** `src/Services/Tenant/Tenant.Application/Features/Commands/CreateTenant/CreateTenantCommandValidator.cs`

```csharp
public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required")
            .Matches(@"^[a-z0-9-]+$").WithMessage("Tenant ID must contain only lowercase letters, numbers, and hyphens");

        RuleFor(x => x.TenantName)
            .NotEmpty().WithMessage("Tenant name is required")
            .MaximumLength(255).WithMessage("Tenant name must not exceed 255 characters");

        RuleFor(x => x.Data)
            .NotNull().WithMessage("Configuration data is required");

        // ✅ Add validation for your new property
        RuleFor(x => x.Data.Email)
            .NotNull().WithMessage("Email configuration is required")
            .When(x => x.Data != null);

        RuleFor(x => x.Data.Email!.Provider)
            .NotEmpty().WithMessage("Email provider is required")
            .When(x => x.Data?.Email != null);

        RuleFor(x => x.Data.Email!.FromEmail)
            .NotEmpty().WithMessage("From email is required")
            .EmailAddress().WithMessage("From email must be a valid email address")
            .When(x => x.Data?.Email != null);
    }
}
```

**Also update:** `UpdateTenantCommandValidator.cs` with the same validation rules.

---

### Step 4: Update Test Data

You need to update test files to include your new property in test configurations.

**Location:** `src/Services/Tenant/Tenant.API.Tests/IntegrationTestBase.cs`

```csharp
protected TenantConfiguration CreateDefaultTenantConfiguration()
{
    return new TenantConfiguration
    {
        Jwt = new JwtSettings
        {
            Secret = "test-jwt-secret-key-minimum-32-characters-long",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        },
        Database = new DatabaseSettings
        {
            Provider = "PostgreSql",
            ConnectionString = "Host=localhost;Database=test_tenant_db;Username=test;Password=test"
        },
        Cors = new CorsSettings
        {
            AllowedOrigins = new List<string> { "https://test.example.com" }
        },
        Otp = new OtpSettings
        {
            ExpiryInMinutes = 5,
            MaxAttempts = 5,
            LockoutDurationInMinutes = 30
        },
        // ✅ Add your new property to test data
        Email = new EmailSettings
        {
            Provider = "SendGrid",
            ApiKey = "test-sendgrid-api-key",
            FromEmail = "noreply@test.com",
            FromName = "Test App"
        }
    };
}
```

---

### Step 5: Run Tests

After adding your property, run the test suite to ensure everything still works:

```bash
cd src/Services/Tenant/Tenant.API.Tests
dotnet test
```

**Expected Result:**

- ✅ All tests should pass
- ✅ No compilation errors
- ✅ New property serializes/deserializes correctly

If tests fail:

- Check JSON serialization/deserialization in `TenantDtos.cs`
- Verify property names match between C# (PascalCase) and JSON (camelCase)
- Ensure `CreateDefaultTenantConfiguration()` includes your new property

---

### Step 6: Test Manually (API)

Test your new property using the Tenant API:

#### **Create Tenant with New Property**

```bash
curl -X POST "https://localhost:5002/api/admin/tenant" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {admin_token}" \
  -d '{
    "tenantId": "test-tenant-123",
    "tenantName": "Test Corp",
    "userId": 1,
    "startDate": "2025-01-01T00:00:00Z",
    "expireDate": "2026-01-01T00:00:00Z",
    "data": {
      "jwt": {
        "secret": "tenant-secret-key-minimum-32-chars",
        "issuer": "TestCorp",
        "audience": "TestCorpApp",
        "accessTokenExpirationMinutes": 60,
        "refreshTokenExpirationDays": 7
      },
      "database": {
        "provider": "PostgreSql",
        "connectionString": "Host=localhost;Database=test_tenant_123_db;..."
      },
      "cors": {
        "allowedOrigins": ["https://testcorp.com"]
      },
      "otp": {
        "expiryInMinutes": 5,
        "maxAttempts": 5,
        "lockoutDurationInMinutes": 30
      },
      "email": {
        "provider": "SendGrid",
        "apiKey": "SG.xxxxxxxxxxxxx",
        "fromEmail": "noreply@testcorp.com",
        "fromName": "TestCorp Support"
      }
    }
  }'
```

#### **Get Tenant Configuration**

```bash
curl -X GET "https://localhost:5002/api/tenant/config/test-tenant-123" \
  -H "Accept: application/json"
```

**Expected Response:**

```json
{
  "tenantId": "test-tenant-123",
  "tenantName": "Test Corp",
  "userId": 1,
  "isActive": true,
  "startDate": "2025-01-01T00:00:00Z",
  "expireDate": "2026-01-01T00:00:00Z",
  "data": {
    "jwt": { ... },
    "database": { ... },
    "cors": { ... },
    "otp": { ... },
    "email": {
      "provider": "SendGrid",
      "apiKey": "SG.xxxxxxxxxxxxx",
      "fromEmail": "noreply@testcorp.com",
      "fromName": "TestCorp Support"
    }
  }
}
```

---

### Step 7: Use the New Property in Your Code

#### **Access New Property from Tenant Context**

```csharp
public class EmailService
{
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<EmailService> _logger;

    public EmailService(ITenantContext tenantContext, ILogger<EmailService> logger)
    {
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        // Get email settings from tenant configuration
        var emailSettings = _tenantContext.CurrentTenant?.Configuration?.Email;

        if (emailSettings == null)
        {
            _logger.LogWarning("Email settings not configured for tenant: {TenantId}",
                _tenantContext.CurrentTenant?.TenantId);
            return false;
        }

        // Use tenant-specific email settings
        switch (emailSettings.Provider)
        {
            case "SendGrid":
                return await SendViaSendGridAsync(emailSettings, to, subject, body);

            case "SMTP":
                return await SendViaSmtpAsync(emailSettings, to, subject, body);

            default:
                _logger.LogError("Unsupported email provider: {Provider}", emailSettings.Provider);
                return false;
        }
    }

    private async Task<bool> SendViaSendGridAsync(EmailSettings settings, string to, string subject, string body)
    {
        var client = new SendGridClient(settings.ApiKey);
        var from = new EmailAddress(settings.FromEmail, settings.FromName);
        var toAddress = new EmailAddress(to);
        var msg = MailHelper.CreateSingleEmail(from, toAddress, subject, body, body);

        var response = await client.SendEmailAsync(msg);

        _logger.LogInformation("SendGrid email sent. Status: {StatusCode}", response.StatusCode);

        return response.StatusCode == System.Net.HttpStatusCode.OK;
    }

    private async Task<bool> SendViaSmtpAsync(EmailSettings settings, string to, string subject, string body)
    {
        if (settings.Smtp == null)
        {
            _logger.LogError("SMTP settings not configured");
            return false;
        }

        using var smtpClient = new SmtpClient(settings.Smtp.Host, settings.Smtp.Port)
        {
            Credentials = new NetworkCredential(settings.Smtp.Username, settings.Smtp.Password),
            EnableSsl = settings.Smtp.UseSsl
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(settings.FromEmail!, settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        mailMessage.To.Add(to);

        await smtpClient.SendMailAsync(mailMessage);

        _logger.LogInformation("SMTP email sent successfully");

        return true;
    }
}
```

---

### Step 8: Update Documentation

Update the relevant documentation files:

1. **MULTI_TENANCY_GUIDE.md** - Add example of new property in "Creating a Tenant" section
2. **DATABASE_PER_TENANT_ARCHITECTURE.md** - Update `TenantConfiguration` structure example
3. **QUICK_REFERENCE.md** - Add new property to tenant configuration examples

**Example update for MULTI_TENANCY_GUIDE.md:**

````markdown
### Tenant Configuration Structure

The `data` field is a JSON object containing tenant-specific settings:

```json
{
  "jwt": { ... },
  "database": { ... },
  "cors": { ... },
  "otp": { ... },
  "email": {
    "provider": "SendGrid",
    "apiKey": "SG.xxxxxxxxxxxxx",
    "fromEmail": "noreply@tenant.com",
    "fromName": "Tenant Support"
  }
}
```
````

````

---

## 🔍 Important Considerations

### JSON Serialization

**C# Property Names (PascalCase) vs JSON (camelCase):**

The system automatically converts between C# property names and JSON field names:

```csharp
// C# Code (PascalCase)
public class TenantConfiguration
{
    public EmailSettings? Email { get; set; }
}

public class EmailSettings
{
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
}
````

```json
// JSON (camelCase)
{
  "email": {
    "fromEmail": "test@example.com",
    "fromName": "Test User"
  }
}
```

**This works because:**

- API layer: `PropertyNameCaseInsensitive = true` accepts both formats
- Response: `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` outputs camelCase

---

### Data Storage in PostgreSQL

Your new property is stored as part of the JSON string in the `Data` column:

```sql
-- TenantSettings table (PostgreSQL)
CREATE TABLE tenant_settings (
    tenant_id VARCHAR(50) PRIMARY KEY,
    tenant_name VARCHAR(255) NOT NULL,
    user_id INT NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    data TEXT NOT NULL,  -- ← JSON string containing ALL configuration
    start_date TIMESTAMP NOT NULL,
    expire_date TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

-- Example data
INSERT INTO tenant_settings (tenant_id, tenant_name, user_id, data)
VALUES (
    'tenant-123',
    'Test Corp',
    1,
    '{"jwt":{...},"database":{...},"cors":{...},"otp":{...},"email":{"provider":"SendGrid","apiKey":"SG.xxx","fromEmail":"test@example.com"}}'
);
```

**Key Points:**

- No database migration needed (data is already JSON text)
- Existing tenants will have `email: null` until updated
- New tenants should include email configuration in create request

---

### Backward Compatibility

When adding new properties, ensure backward compatibility:

```csharp
// ✅ GOOD: Nullable property (won't break existing tenants)
public EmailSettings? Email { get; set; }

// ❌ BAD: Required property (breaks existing tenants)
public required EmailSettings Email { get; set; }
```

**Handling Missing Properties:**

```csharp
public async Task<bool> SendEmailAsync(string to, string subject, string body)
{
    var emailSettings = _tenantContext.CurrentTenant?.Configuration?.Email;

    // ✅ Gracefully handle missing email settings
    if (emailSettings == null)
    {
        _logger.LogWarning("Email settings not configured for tenant: {TenantId}. Falling back to default.",
            _tenantContext.CurrentTenant?.TenantId);

        // Option 1: Use default settings
        emailSettings = GetDefaultEmailSettings();

        // Option 2: Skip email functionality
        return false;
    }

    // Continue with email sending...
}
```

---

### Cache Invalidation

After adding a new property and updating tenant configurations, **clear the tenant cache** to ensure services get the latest configuration:

```csharp
// In your update handler or admin endpoint
private readonly ITenantConfigurationProvider _tenantConfigProvider;

public async Task UpdateTenantAsync(string tenantId, TenantConfiguration newConfig)
{
    // Update tenant in database
    await _tenantRepository.UpdateAsync(tenantId, newConfig);

    // ✅ Clear cache to force reload
    _tenantConfigProvider.ClearCache(tenantId);

    _logger.LogInformation("Tenant configuration updated and cache cleared: {TenantId}", tenantId);
}
```

**Or clear all tenant caches:**

```csharp
_tenantConfigProvider.ClearAllCache();
```

---

## 📊 Example: Adding SMS Provider Settings

Let's walk through a complete example of adding SMS provider settings.

### 1. Create SmsSettings Class

```csharp
// File: src/Shared/IhsanDev.Shared.Kernel/Dto/Tenant/SmsSettings.cs

namespace IhsanDev.Shared.Kernel.Dto.Tenant;

public class SmsSettings
{
    public string? Provider { get; set; }          // "Twilio", "AWS_SNS", "Nexmo"
    public string? AccountSid { get; set; }        // Twilio account SID
    public string? AuthToken { get; set; }         // Twilio auth token
    public string? FromNumber { get; set; }        // Default sender phone number
    public AwsSnsSettings? AwsSns { get; set; }    // AWS SNS-specific settings
}

public class AwsSnsSettings
{
    public string? Region { get; set; }            // AWS region
    public string? AccessKeyId { get; set; }       // AWS access key
    public string? SecretAccessKey { get; set; }   // AWS secret key
}
```

### 2. Add to TenantConfiguration

```csharp
public class TenantConfiguration
{
    public JwtSettings? Jwt { get; set; }
    public DatabaseSettings? Database { get; set; }
    public CorsSettings? Cors { get; set; }
    public OtpSettings? Otp { get; set; }
    public EmailSettings? Email { get; set; }
    public SmsSettings? Sms { get; set; }  // ✅ New property
}
```

### 3. Update Test Data

```csharp
protected TenantConfiguration CreateDefaultTenantConfiguration()
{
    return new TenantConfiguration
    {
        // ... existing properties ...
        Sms = new SmsSettings
        {
            Provider = "Twilio",
            AccountSid = "test-account-sid",
            AuthToken = "test-auth-token",
            FromNumber = "+15551234567"
        }
    };
}
```

### 4. Add Validation (Optional)

```csharp
public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        // ... existing validation ...

        RuleFor(x => x.Data.Sms!.Provider)
            .NotEmpty().WithMessage("SMS provider is required")
            .When(x => x.Data?.Sms != null);

        RuleFor(x => x.Data.Sms!.FromNumber)
            .NotEmpty().WithMessage("From number is required")
            .Matches(@"^\+\d{10,15}$").WithMessage("From number must be in E.164 format (+1234567890)")
            .When(x => x.Data?.Sms != null);
    }
}
```

### 5. Use in Your Code

```csharp
public class SmsService
{
    private readonly ITenantContext _tenantContext;

    public async Task<bool> SendSmsAsync(string to, string message)
    {
        var smsSettings = _tenantContext.CurrentTenant?.Configuration?.Sms;

        if (smsSettings == null)
        {
            throw new InvalidOperationException("SMS settings not configured for this tenant");
        }

        switch (smsSettings.Provider)
        {
            case "Twilio":
                return await SendViaTwilioAsync(smsSettings, to, message);

            case "AWS_SNS":
                return await SendViaAwsSnsAsync(smsSettings, to, message);

            default:
                throw new NotSupportedException($"SMS provider '{smsSettings.Provider}' is not supported");
        }
    }

    private async Task<bool> SendViaTwilioAsync(SmsSettings settings, string to, string message)
    {
        TwilioClient.Init(settings.AccountSid, settings.AuthToken);

        var messageResource = await MessageResource.CreateAsync(
            to: new PhoneNumber(to),
            from: new PhoneNumber(settings.FromNumber!),
            body: message
        );

        return messageResource.Status != MessageResource.StatusEnum.Failed;
    }

    private async Task<bool> SendViaAwsSnsAsync(SmsSettings settings, string to, string message)
    {
        if (settings.AwsSns == null)
        {
            throw new InvalidOperationException("AWS SNS settings not configured");
        }

        var credentials = new BasicAWSCredentials(
            settings.AwsSns.AccessKeyId,
            settings.AwsSns.SecretAccessKey
        );

        var snsClient = new AmazonSimpleNotificationServiceClient(
            credentials,
            RegionEndpoint.GetBySystemName(settings.AwsSns.Region)
        );

        var publishRequest = new PublishRequest
        {
            Message = message,
            PhoneNumber = to
        };

        var response = await snsClient.PublishAsync(publishRequest);

        return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }
}
```

---

## ✅ Checklist

Before completing your changes, verify:

- [ ] Added property to `TenantConfiguration.cs`
- [ ] Created settings class (if needed) with proper naming (PascalCase)
- [ ] Used nullable types for backward compatibility
- [ ] Updated `CreateDefaultTenantConfiguration()` in test base
- [ ] Added validation rules (if needed)
- [ ] Ran all tests (`dotnet test`)
- [ ] Tested create/update/get tenant via API
- [ ] Verified JSON serialization (camelCase in JSON, PascalCase in C#)
- [ ] Updated documentation (MULTI_TENANCY_GUIDE.md, etc.)
- [ ] Implemented service to use the new property
- [ ] Added error handling for missing/null configurations
- [ ] Cleared tenant cache after updates

---

## 🚨 Common Pitfalls

### 1. Forgetting Nullable Types

```csharp
// ❌ BAD: Breaks existing tenants without email settings
public required EmailSettings Email { get; set; }

// ✅ GOOD: Allows null for backward compatibility
public EmailSettings? Email { get; set; }
```

### 2. Case Sensitivity Issues

```csharp
// C# Property (PascalCase)
public string? FromEmail { get; set; }

// JSON must use camelCase (automatic with JsonNamingPolicy.CamelCase)
{
  "fromEmail": "test@example.com"  // ✅ Correct
}

// This will NOT work without PropertyNameCaseInsensitive:
{
  "FromEmail": "test@example.com"  // ❌ Wrong (but currently allowed due to PropertyNameCaseInsensitive)
}
```

### 3. Not Updating Test Data

```csharp
// Tests will fail if you forget to update CreateDefaultTenantConfiguration()
protected TenantConfiguration CreateDefaultTenantConfiguration()
{
    return new TenantConfiguration
    {
        Jwt = new JwtSettings { ... },
        Database = new DatabaseSettings { ... },
        // ❌ Forgot to add Email property - tests will fail!
    };
}
```

### 4. Forgetting Cache Invalidation

```csharp
// After updating tenant configuration, always clear cache
public async Task UpdateTenantAsync(UpdateTenantCommand request)
{
    await _repository.UpdateAsync(request.TenantId, request.Data);

    // ✅ Clear cache so services get fresh configuration
    _tenantConfigProvider.ClearCache(request.TenantId);
}
```

---

## 📚 Related Documentation

- **MULTI_TENANCY_GUIDE.md** - Complete multi-tenancy architecture overview
- **DATABASE_PER_TENANT_ARCHITECTURE.md** - Database isolation strategy
- **QUICK_REFERENCE.md** - Quick reference for common multi-tenancy tasks

---

**Last Updated:** January 30, 2025  
**Version:** 1.0.0
