# 🌍 Translation Service - Complete Guide

**Version:** 1.1  
**Last Updated:** January 27, 2026  
**Port:** 5006  
**Database Pattern:** Global Database with Optional Tenant Context  
**Test Status:** ✅ 45/45 Tests Passing (100%)

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Key Features](#key-features)
4. [Database Design](#database-design)
5. [API Endpoints](#api-endpoints)
6. [Integration Guide](#integration-guide)
7. [Usage Examples](#usage-examples)
8. [Configuration](#configuration)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)

---

## Overview

### What is Translation Service?

The Translation Service manages multi-language translations across the entire microservices architecture. It supports:

- ✅ **Global translations** - Accessible to all tenants (TenantId = null)
- ✅ **Tenant-specific overrides** - Custom translations per tenant (TenantId != null)
- ✅ **Optional tenant context** - Works with or without `x-tenant-id` header
- ✅ **Multiple languages** - Support for any number of languages
- ✅ **Category-based organization** - Group translations by category
- ✅ **Admin management** - CRUD operations for translation keys and values

### Service Classification

```
┌─────────────────────────────────────────────────────────────────┐
│ Translation Service Classification:                             │
│                                                                  │
│ Type: SHARED SERVICE (Provider)                                 │
│ Multi-Tenancy: GLOBAL DATABASE (like Tenant Service)            │
│ Pattern: Single database with TenantId column                   │
│ Port: 5006                                                       │
│                                                                  │
│ ✅ Stores translations for ALL tenants in one database          │
│ ✅ TenantId column for tenant-specific overrides                │
│ ✅ Global translations (TenantId = null) for all tenants        │
│ ✅ Optional x-tenant-id header support                          │
└─────────────────────────────────────────────────────────────────┘
```

### Similar Services

Translation Service follows the same pattern as:

- **Identity Service** - Global database, optional tenant context
- **FileManager Service** - Global database, optional tenant context
- **Notification Service** - Global database, optional tenant context

---

## Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    Translation Service                           │
│                     (Port: 5006)                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │         Global Database (PostgreSQL)                   │    │
│  │                                                          │    │
│  │  TranslationKeys                                        │    │
│  │  ├─ Id, Key, Category, Description                     │    │
│  │  └─ (No TenantId - shared across tenants)              │    │
│  │                                                          │    │
│  │  TranslationValues                                      │    │
│  │  ├─ Id, TranslationKeyId, Language, Value              │    │
│  │  └─ TenantId (nullable) ← Tenant overrides             │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────┐       ┌─────────────────┐      ┌──────────────┐
│ Frontend Apps   │       │ Identity Service│      │Tenant Service│
│ (Angular/React) │◄──────┤ (Auth & Users)  │      │ (Config)     │
└─────────────────┘       └─────────────────┘      └──────────────┘
        │                         │                        │
        │                         │                        │
        └─────────────────────────┼────────────────────────┘
                                  │
                                  ▼
                        Translation Service
                        • Gets translations
                        • Applies overrides
                        • Returns merged result
```

### How It Works

#### Without `x-tenant-id` Header (Global Translations)

```
1. Request: GET /api/translations/en
   Headers: (none)

2. Translation Service:
   - Queries: WHERE Language = 'en' AND TenantId IS NULL
   - Returns: Only global translations

3. Response:
   {
     "language": "en",
     "translations": {
       "welcome.message": "Welcome to our application",
       "login.button": "Login"
     }
   }
```

#### With `x-tenant-id` Header (Global + Tenant Overrides)

```
1. Request: GET /api/translations/en
   Headers: x-tenant-id: tenant-123

2. Translation Service:
   - Queries: WHERE Language = 'en' AND (TenantId IS NULL OR TenantId = 'tenant-123')
   - Merges: Global translations + Tenant overrides
   - Priority: Tenant-specific values override global ones

3. Response:
   {
     "language": "en",
     "translations": {
       "welcome.message": "Welcome to Acme Corp",  ← Tenant override
       "login.button": "Login"  ← Global value
     }
   }
```

---

## Key Features

### 1. Global Translations

Global translations are available to all tenants and serve as defaults.

- **TenantId**: `null`
- **Access**: Available to all tenants
- **Purpose**: Default translations, common messages
- **Management**: Admin-only creation and updates

### 2. Tenant-Specific Overrides

Tenants can customize specific translations without affecting others.

- **TenantId**: Specific tenant identifier
- **Access**: Only visible to that tenant
- **Purpose**: Branding, custom messaging, localization
- **Priority**: Overrides global translations for the same key

### 3. Multi-Language Support

Unlimited language support with flexible language codes.

- **Format**: `en`, `ar`, `fr`, `de`, etc.
- **Extensible**: Add any language
- **Validation**: Language code required for all translations

### 4. Category-Based Organization

Organize translations into logical categories.

- **Examples**: `General`, `Validation`, `Error`, `Success`, `Navigation`
- **Filtering**: Query translations by category
- **Organization**: Easier management and updates

### 5. Admin Management

Complete CRUD operations for administrators.

- **Role Required**: `Admin` or `SuperAdmin`
- **Operations**: Create keys, update translations, delete entries
- **Pagination**: Efficient browsing of translation keys
- **Search**: Find translations by key or category

---

## Database Design

### Entity-Relationship Diagram

```
┌─────────────────────────────────────────┐
│ TranslationKey                          │
├─────────────────────────────────────────┤
│ Id              INT (PK)                │
│ Key             VARCHAR(200) UNIQUE     │
│ Category        VARCHAR(100)            │
│ Description     VARCHAR(500) NULLABLE   │
│ IsActive        BOOLEAN                 │
│ Created         DATETIME                │
│ LastModified    DATETIME NULLABLE       │
│ Status          BOOLEAN                 │
└─────────────────────────────────────────┘
                    │
                    │ 1:N
                    ▼
┌─────────────────────────────────────────┐
│ TranslationValue                        │
├─────────────────────────────────────────┤
│ Id              INT (PK)                │
│ TranslationKeyId INT (FK)               │
│ Language        VARCHAR(10)             │
│ Value           TEXT                    │
│ TenantId        VARCHAR(450) NULLABLE   │ ← Optional tenant ID
│ Created         DATETIME                │
│ LastModified    DATETIME NULLABLE       │
└─────────────────────────────────────────┘

Unique Constraint: (TranslationKeyId, Language, TenantId)
Index: TenantId (for efficient tenant queries)
Index: Language (for language queries)
```

### Key Design Decisions

#### 1. Why Separate Tables?

- **TranslationKey**: Defines available translation keys (shared across languages)
- **TranslationValue**: Stores actual translations per language/tenant
- **Benefits**: Easier to add languages, better normalization, cleaner queries

#### 2. Why Nullable TenantId?

```sql
-- Global translation (all tenants)
TenantId = NULL

-- Tenant-specific override
TenantId = 'tenant-123'

-- Query for tenant with fallback to global
WHERE Language = 'en'
  AND (TenantId IS NULL OR TenantId = @tenantId)
```

#### 3. Composite Unique Constraint

```sql
UNIQUE (TranslationKeyId, Language, TenantId)
```

**Ensures:**

- One global translation per key + language
- One tenant override per key + language + tenant
- Prevents duplicate translations

---

## API Endpoints

### Public Endpoints (No Authentication Required)

#### GET /api/translations/{language}

Get all translations for a specific language.

**Parameters:**

- `language` (path, required): Language code (e.g., `en`, `ar`)
- `category` (query, optional): Filter by category
- `x-tenant-id` (header, optional): Tenant identifier for overrides

**Responses:**

```http
GET /api/translations/en
```

```json
{
  "language": "en",
  "category": null,
  "tenantId": null,
  "translations": {
    "welcome.message": "Welcome",
    "login.button": "Login",
    "error.required": "{0} is required"
  }
}
```

**With Tenant Override:**

```http
GET /api/translations/en
x-tenant-id: tenant-123
```

```json
{
  "language": "en",
  "category": null,
  "tenantId": "tenant-123",
  "translations": {
    "welcome.message": "Welcome to Acme Corp",  ← Tenant override
    "login.button": "Login",
    "error.required": "{0} is required"
  }
}
```

**With Category Filter:**

```http
GET /api/translations/en?category=Validation
```

```json
{
  "language": "en",
  "category": "Validation",
  "tenantId": null,
  "translations": {
    "error.required": "{0} is required",
    "error.maxLength": "{0} cannot exceed {1} characters",
    "error.email": "{0} must be a valid email"
  }
}
```

---

### Admin Endpoints (Require Admin/SuperAdmin Role)

#### GET /api/translations/keys

Get paginated list of translation keys.

**Authorization:** Bearer token with `Admin` or `SuperAdmin` role

**Parameters:**

- `pageNumber` (query, optional): Page number (default: 1)
- `pageSize` (query, optional): Items per page (default: 10)
- `category` (query, optional): Filter by category
- `searchTerm` (query, optional): Search in key names
- `isActive` (query, optional): Filter by active status

**Response:**

```http
GET /api/translations/keys?pageNumber=1&pageSize=10&category=General
Authorization: Bearer {jwt-token}
```

```json
{
  "items": [
    {
      "id": 1,
      "key": "welcome.message",
      "category": "General",
      "description": "Welcome message shown on homepage",
      "isActive": true,
      "created": "2026-01-15T10:30:00Z",
      "lastModified": null
    },
    {
      "id": 2,
      "key": "login.button",
      "category": "General",
      "description": "Login button text",
      "isActive": true,
      "created": "2026-01-15T10:31:00Z",
      "lastModified": null
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 2,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

#### POST /api/translations/keys

Create a new translation key.

**Authorization:** Bearer token with `Admin` or `SuperAdmin` role

**Request Body:**

```json
{
  "key": "app.title",
  "category": "General",
  "description": "Application title displayed in header"
}
```

**Response:** `201 Created`

```json
{
  "id": 3,
  "key": "app.title",
  "category": "General",
  "description": "Application title displayed in header",
  "isActive": true,
  "created": "2026-01-27T14:20:00Z",
  "lastModified": null
}
```

#### PUT /api/translations/keys/{id}

Update a translation key's description.

**Authorization:** Bearer token with `Admin` or `SuperAdmin` role

**Request Body:**

```json
{
  "id": 3,
  "description": "Updated description for application title"
}
```

**Response:** `200 OK`

```json
{
  "id": 3,
  "key": "app.title",
  "category": "General",
  "description": "Updated description for application title",
  "isActive": true,
  "created": "2026-01-27T14:20:00Z",
  "lastModified": "2026-01-27T14:25:00Z"
}
```

#### DELETE /api/translations/keys/{id}

Delete a translation key (and all its translations).

**Authorization:** Bearer token with `Admin` or `SuperAdmin` role

**Response:** `204 No Content`

#### POST /api/translations/values

Set or update a translation value.

**Authorization:** Bearer token with `Admin` or `SuperAdmin` role

**Request Body (Global Translation):**

```json
{
  "key": "app.title",
  "language": "en",
  "value": "My Application",
  "category": "General",
  "tenantId": null
}
```

**Request Body (Tenant-Specific Override):**

```json
{
  "key": "app.title",
  "language": "en",
  "value": "Acme Corp Portal",
  "category": "General",
  "tenantId": "tenant-123"
}
```

**Response:** `200 OK`

```json
{
  "id": 5,
  "translationKeyId": 3,
  "key": "app.title",
  "language": "en",
  "value": "My Application",
  "tenantId": null,
  "created": "2026-01-27T14:30:00Z",
  "lastModified": null
}
```

#### POST /api/translations/import

Bulk import translations from JSON.

**Authorization:** Bearer token with `Admin` or `SuperAdmin` role

**Request Body:**

```json
{
  "language": "en",
  "tenantId": null,
  "category": "General",
  "translations": {
    "welcome.message": "Welcome to our app",
    "login.button": "Login",
    "logout.button": "Logout"
  }
}
```

**Response:** `200 OK`

```json
{
  "imported": 3,
  "updated": 0,
  "errors": []
}
```

---

## Integration Guide

### Frontend Integration (Angular/React)

#### Step 1: Create Translation Service

```typescript
// services/translation.service.ts
import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";

@Injectable({
  providedIn: "root",
})
export class TranslationService {
  private apiUrl = "http://localhost:5006/api/translations";

  constructor(private http: HttpClient) {}

  getTranslations(
    language: string,
    tenantId?: string,
  ): Observable<TranslationsDto> {
    const headers = tenantId ? { "x-tenant-id": tenantId } : {};
    return this.http.get<TranslationsDto>(`${this.apiUrl}/${language}`, {
      headers,
    });
  }

  getTranslationsByCategory(
    language: string,
    category: string,
    tenantId?: string,
  ): Observable<TranslationsDto> {
    const headers = tenantId ? { "x-tenant-id": tenantId } : {};
    return this.http.get<TranslationsDto>(
      `${this.apiUrl}/${language}?category=${category}`,
      { headers },
    );
  }
}

interface TranslationsDto {
  language: string;
  category: string | null;
  tenantId: string | null;
  translations: { [key: string]: string };
}
```

#### Step 2: Use in Components

```typescript
// app.component.ts
export class AppComponent implements OnInit {
  translations: { [key: string]: string } = {};

  constructor(private translationService: TranslationService) {}

  ngOnInit() {
    const language = localStorage.getItem("language") || "en";
    const tenantId = localStorage.getItem("tenantId");

    this.translationService
      .getTranslations(language, tenantId)
      .subscribe((response) => {
        this.translations = response.translations;
      });
  }

  translate(key: string, ...params: any[]): string {
    let value = this.translations[key] || key;

    // Replace {0}, {1}, etc. with parameters
    params.forEach((param, index) => {
      value = value.replace(`{${index}}`, param);
    });

    return value;
  }
}
```

```html
<!-- app.component.html -->
<h1>{{ translate('welcome.message') }}</h1>
<button>{{ translate('login.button') }}</button>
<p>{{ translate('error.required', 'Email') }}</p>
```

### Backend Integration (.NET Services)

#### Step 1: Add HTTP Client

```csharp
// Program.cs
builder.Services.AddHttpClient("TranslationService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5006");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
```

#### Step 2: Create Service Interface

```csharp
// ITranslationServiceClient.cs
public interface ITranslationServiceClient
{
    Task<TranslationsDto> GetTranslationsAsync(
        string language,
        string? tenantId = null,
        string? category = null,
        CancellationToken cancellationToken = default);
}

public record TranslationsDto(
    string Language,
    string? Category,
    string? TenantId,
    Dictionary<string, string> Translations
);
```

#### Step 3: Implement Client

```csharp
// TranslationServiceClient.cs
public class TranslationServiceClient : ITranslationServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TranslationServiceClient> _logger;

    public TranslationServiceClient(
        IHttpClientFactory httpClientFactory,
        ILogger<TranslationServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<TranslationsDto> GetTranslationsAsync(
        string language,
        string? tenantId = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("TranslationService");

        var url = $"/api/translations/{language}";
        if (!string.IsNullOrEmpty(category))
            url += $"?category={category}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(tenantId))
            request.Headers.Add("x-tenant-id", tenantId);

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TranslationsDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize translations");
    }
}
```

#### Step 4: Register and Use

```csharp
// Program.cs
builder.Services.AddScoped<ITranslationServiceClient, TranslationServiceClient>();

// In your handlers
public class SendEmailHandler
{
    private readonly ITranslationServiceClient _translationClient;

    public async Task Handle(SendEmailCommand request, CancellationToken ct)
    {
        var translations = await _translationClient.GetTranslationsAsync(
            language: request.Language,
            tenantId: request.TenantId,
            cancellationToken: ct
        );

        var emailSubject = translations.Translations["email.subject"];
        var emailBody = translations.Translations["email.body"];

        // Send email with translated content
    }
}
```

---

## Configuration

### appsettings.json

```json
{
  "Urls": "http://localhost:5006",

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    },
    "FilePath": "C:\\Logs"
  },

  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=global;Username=postgres;Password=yourpassword;Minimum Pool Size=5;Maximum Pool Size=50;Connection Idle Lifetime=300;Connection Pruning Interval=10;Pooling=true;",
    "EnableSensitiveDataLogging": false,
    "EnableDetailedErrors": false,
    "CommandTimeout": 30,
    "MaxRetryCount": 3,
    "MaxRetryDelay": 30
  },

  "Jwt": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters-long",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 21600,
    "RefreshTokenExpirationDays": 7
  },

  "MultiTenancy": {
    "Enabled": false,
    "TenantServiceUrl": "http://localhost:5002",
    "CacheExpirationMinutes": 5,
    "JwtMode": "Shared"
  },

  "Cors": {
    "AllowedOrigins": ["http://localhost:4200", "http://localhost:5001"]
  },

  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  },

  "RateLimiting": {
    "Global": {
      "PermitLimit": 20000,
      "WindowMinutes": 1
    },
    "PerIP": {
      "PermitLimit": 200,
      "WindowMinutes": 1
    }
  }
}
```

---

## Best Practices

### 1. Translation Key Naming

**Use dot notation for hierarchical keys:**

```
✅ Good:
- error.required
- error.maxLength
- validation.email.invalid
- navigation.home
- navigation.profile

❌ Bad:
- ERROR_REQUIRED
- maxLengthError
- EmailValidation
```

### 2. Category Organization

**Organize by functional area:**

- `General` - Common messages, labels
- `Validation` - Form validation messages
- `Error` - Error messages
- `Success` - Success messages
- `Navigation` - Menu items, links
- `Email` - Email templates
- `Notification` - Push notifications

### 3. Parameter Placeholders

**Use numbered placeholders for dynamic values:**

```json
{
  "error.required": "{0} is required",
  "error.maxLength": "{0} cannot exceed {1} characters",
  "welcome.user": "Welcome, {0}! You have {1} new messages"
}
```

**Usage:**

```typescript
translate("error.required", "Email"); // "Email is required"
translate("error.maxLength", "Username", "50"); // "Username cannot exceed 50 characters"
```

### 4. Fallback Strategy

**Always provide global translations as fallback:**

1. Create global translations first (TenantId = null)
2. Add tenant overrides only when needed
3. Frontend merges: Tenant override > Global > Key name

### 5. Caching

**Translation service uses Redis caching:**

- Cache key: `MicroservicesApp:Translations:{language}:{tenantId?}`
- Expiration: 5 minutes (configurable)
- Auto-refresh: On translation updates

**Manual cache invalidation:**

```http
DELETE /api/cache/translations/{language}?tenantId={tenantId}
```

---

## Troubleshooting

### Issue: Translations Not Updating

**Symptoms:** Changes to translations not reflected in frontend

**Causes:**

1. Redis cache not cleared
2. Frontend caching translations
3. Wrong tenant ID in request

**Solutions:**

```bash
# Clear Redis cache
redis-cli FLUSHDB

# Or clear specific translation cache
DELETE /api/cache/translations/en?tenantId=tenant-123

# Check frontend localStorage/sessionStorage
localStorage.removeItem('translations');
```

### Issue: Missing Tenant Overrides

**Symptoms:** Tenant-specific translations not showing

**Causes:**

1. Missing `x-tenant-id` header
2. Tenant override not created
3. TenantId mismatch

**Solutions:**

```typescript
// Verify header is sent
this.http.get(url, {
  headers: { 'x-tenant-id': 'tenant-123' }
});

// Check translation exists for tenant
GET /api/translations/keys?searchTerm=welcome.message
// Verify entry exists with correct TenantId
```

### Issue: 401 Unauthorized on Admin Endpoints

**Symptoms:** Can't create/update translations

**Causes:**

1. Missing JWT token
2. User doesn't have Admin role
3. JWT expired

**Solutions:**

```http
# Verify JWT has Admin role
GET /api/user/profile
Authorization: Bearer {token}

# Check token expiration
# JWT tokens expire after AccessTokenExpirationMinutes

# Login again to get fresh token
POST /api/auth/login
```

---

## Performance Considerations

### Database Indexes

```sql
-- Already created by EF Core migrations
CREATE INDEX IX_TranslationKeys_Key ON TranslationKeys(Key);
CREATE INDEX IX_TranslationKeys_Category ON TranslationKeys(Category);
CREATE INDEX IX_TranslationKeys_IsActive ON TranslationKeys(IsActive);
CREATE INDEX IX_TranslationValues_Key_Lang_Tenant ON TranslationValues(TranslationKeyId, Language, TenantId);
CREATE INDEX IX_TranslationValues_TenantId ON TranslationValues(TenantId);
CREATE INDEX IX_TranslationValues_Language ON TranslationValues(Language);
```

### Query Optimization

**Efficient query for tenant translations:**

```sql
-- Single query fetches global + tenant translations
SELECT tv.*, tk.Key
FROM TranslationValues tv
INNER JOIN TranslationKeys tk ON tv.TranslationKeyId = tk.Id
WHERE tv.Language = @Language
  AND (tv.TenantId IS NULL OR tv.TenantId = @TenantId)
  AND tk.IsActive = TRUE
ORDER BY tv.TenantId DESC; -- Tenant overrides first
```

### Caching Strategy

1. **Redis Cache**: 95%+ hit rate for frequently accessed translations
2. **Cache Invalidation**: Automatic on translation updates
3. **Cache Warmup**: Pre-load common languages on startup
4. **TTL**: 5 minutes (configurable via `MultiTenancy:CacheExpirationMinutes`)

---

## Related Documentation

- [TRANSLATION_SERVICE_QUICK_REFERENCE.md](TRANSLATION_SERVICE_QUICK_REFERENCE.md) - Quick API reference
- [TRANSLATION_SERVICE_TEST_FIX_SUMMARY.md](TRANSLATION_SERVICE_TEST_FIX_SUMMARY.md) - Test infrastructure & fixes
- [TRANSLATION_SERVICE_FINAL_VERIFICATION.md](TRANSLATION_SERVICE_FINAL_VERIFICATION.md) - Design pattern verification
- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Multi-tenancy architecture
- [IDENTITY_OPTIONAL_TENANT_IMPLEMENTATION_SUMMARY.md](IDENTITY_OPTIONAL_TENANT_IMPLEMENTATION_SUMMARY.md) - Optional tenant pattern
- [FILE_MANAGER_SERVICE_GUIDE.md](FILE_MANAGER_SERVICE_GUIDE.md) - Similar service pattern
- [LOCALIZATION_GUIDE.md](LOCALIZATION_GUIDE.md) - Application localization
- [SHARED_TESTING_FILES.md](SHARED_TESTING_FILES.md) - Testing best practices
- [00_START_HERE.md](00_START_HERE.md) - Architecture overview

---

**Last Updated:** January 27, 2026  
**Version:** 1.1  
**Status:** ✅ Production Ready | ✅ Tests: 45/45 Passing
