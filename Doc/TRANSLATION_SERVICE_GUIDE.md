# 🌍 Translation Service - Complete Guide

**Version:** 1.3  
**Last Updated:** August 13, 2026  
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
│  │  └─ TenantId (nullable) ← NULL = global key            │    │
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
1. Request: GET /api/v1/translations/en
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
1. Request: GET /api/v1/translations/en
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
│ Key             VARCHAR(200)            │
│ Category        VARCHAR(100)            │
│ Description     VARCHAR(500) NULLABLE   │
│ IsActive        BOOLEAN                 │
│ TenantId        VARCHAR(450) NULLABLE   │ ← NULL = global key, non-null = tenant-owned key
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
│ TenantId        VARCHAR(450) NULLABLE   │ ← NULL = global value, non-null = tenant override
│ Created         DATETIME                │
│ LastModified    DATETIME NULLABLE       │
└─────────────────────────────────────────┘

Unique Constraint (TranslationKey):   (Key, TenantId) WHERE IsArchived = false
Unique Constraint (TranslationValue): (TranslationKeyId, Language, TenantId)
Index: TenantId on both tables (for efficient tenant queries)
Index: Language (for language queries)
```

Both entities carry their own nullable `TenantId` today, not just `TranslationValue`:

- `TranslationKey.TenantId` (`Translation.Domain/Entities/TranslationKey.cs`) — `null` for a
  global key created via `TranslationKey.Create(key, category, description)`, or set to a real
  tenant id for a key created via `TranslationKey.CreateForTenant(key, category, tenantId, description)`.
  This makes a key itself genuinely tenant-owned (e.g. every key imported with a `#tenantId#`
  prefix that doesn't already exist globally), not merely a shared key with tenant-scoped
  *values* layered on top.
- `TranslationValue.TenantId` — unchanged from before: `null` for a global value, a tenant id for
  a per-tenant override of a (usually global) key's translation.

In practice a tenant-owned key still needs at least one `TranslationValue` row (also tenant-scoped)
to have any actual translated text — `TranslationKey.TenantId` controls who the *key* belongs to,
`TranslationValue.TenantId` controls who a specific language's *text* belongs to.

### Key Design Decisions

#### 1. Why Separate Tables?

- **TranslationKey**: Defines available translation keys (shared across languages)
- **TranslationValue**: Stores actual translations per language/tenant
- **Benefits**: Easier to add languages, better normalization, cleaner queries

#### 2. Why Nullable TenantId?

Both `TranslationKey` and `TranslationValue` carry a nullable `TenantId` — not just
`TranslationValue` as an earlier version of this doc stated:

```sql
-- Global translation key/value (all tenants)
TenantId = NULL

-- Tenant-owned key or tenant-specific value override
TenantId = 'tenant-123'

-- Query for tenant with fallback to global
WHERE Language = 'en'
  AND (TenantId IS NULL OR TenantId = @tenantId)
```

`TranslationKey.TenantId` lets a key itself be genuinely tenant-owned (via the
`TranslationKey.CreateForTenant` factory), separately from `TranslationValue.TenantId`, which lets
any (usually global) key have a per-tenant override of its translated text (via
`TranslationValue.CreateTenantOverride`).

#### 3. Unique Constraints

```sql
-- TranslationKey: one key string per tenant scope, ignoring archived rows
UNIQUE (Key, TenantId) WHERE IsArchived = false  -- IX_TranslationKeys_Key_TenantId

-- TranslationValue: one value per key + language + tenant
UNIQUE (TranslationKeyId, Language, TenantId)  -- IX_TranslationValues_Key_Lang_Tenant
```

**Ensures:**

- One global key (or one tenant-owned key with the same string) per key + tenant scope, and a
  deleted (archived) key doesn't block re-creating the same key string (see Troubleshooting below)
- One global translation per key + language
- One tenant override per key + language + tenant
- Prevents duplicate translations

---

## API Endpoints

### Public Endpoints (No Authentication Required)

#### GET /api/v1/translations/{language}

Get all translations for a specific language.

**Parameters:**

- `language` (path, required): Language code (e.g., `en`, `ar`)
- `category` (query, optional): Filter by category
- `x-tenant-id` (header, optional): Tenant identifier for overrides

**Responses:**

Note: `TranslationsDto` (`Translation.Application/DTOs/TranslationsDto.cs`) has no `category`
field — the requested category filter is not echoed back in the response. It does have a
`cachedAt` field (`GetTranslationsQueryHandler` sets it to the UTC time the response was
computed/cached), shown below.

```http
GET /api/v1/translations/en
```

```json
{
  "language": "en",
  "tenantId": null,
  "translations": {
    "welcome.message": "Welcome",
    "login.button": "Login",
    "error.required": "{0} is required"
  },
  "cachedAt": "2026-08-13T10:15:00Z"
}
```

**With Tenant Override:**

```http
GET /api/v1/translations/en
x-tenant-id: tenant-123
```

```json
{
  "language": "en",
  "tenantId": "tenant-123",
  "translations": {
    "welcome.message": "Welcome to Acme Corp",  ← Tenant override
    "login.button": "Login",
    "error.required": "{0} is required"
  },
  "cachedAt": "2026-08-13T10:15:00Z"
}
```

**With Category Filter:**

```http
GET /api/v1/translations/en?category=Validation
```

```json
{
  "language": "en",
  "tenantId": null,
  "translations": {
    "error.required": "{0} is required",
    "error.maxLength": "{0} cannot exceed {1} characters",
    "error.email": "{0} must be a valid email"
  },
  "cachedAt": "2026-08-13T10:15:00Z"
}
```

---

### Admin Endpoints (Require Admin/SuperAdmin Role)

#### GET /api/v1/translations/keys

Get paginated list of translation keys.

**Authorization:** Bearer token with `Admin` or `SuperAdmin` role

**Parameters:**

- `pageNumber` (query, optional): Page number (default: 1)
- `pageSize` (query, optional): Items per page (default: 10, max: 100)
- `category` (query, optional): Filter by category (substring match)
- `tenantId` (query, optional): Include this tenant's keys alongside global keys. Also read from
  the `x-tenant-id` header if not passed as a query param (`TranslationApiHandlers.GetTranslationKeysHandler`).
  Omitted → only global keys (`TenantId == null`) are returned.
- `searchTerm` (query, optional): Search in key name or description
- `isArchived` (query, optional): Filter by archived status (default: `false`) — there is no
  `isActive` filter; `GetTranslationKeysQuery.IsArchived` is the real parameter name
  (`Translation.Application/Queries/GetTranslationKeysQuery.cs`)

**Response:**

```http
GET /api/v1/translations/keys?pageNumber=1&pageSize=10&category=General
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

#### POST /api/v1/translations/keys

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

#### PUT /api/v1/translations/keys/{id}

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

#### DELETE /api/v1/translations/keys/{id}

Delete a translation key (and all its translations).

**Authorization:** Bearer token with `Admin` or `SuperAdmin` role

**Response:** `204 No Content`

#### POST /api/v1/translations/values

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

#### DELETE /api/v1/translations/values/{id}

Delete a specific translation value without deleting the entire key.

**Authorization:** Bearer token with `Admin` or `SuperAdmin` role

**Usage Example:**

```bash
# Delete a specific translation value (e.g., remove Arabic translation but keep English)
DELETE /api/v1/translations/values/5
```

**Use Cases:**

- Remove a translation for a specific language
- Delete a tenant-specific override to fall back to global translation
- Clean up obsolete translations

**Response:** `204 No Content`

**Note:** This deletes only the translation value, not the translation key. To delete the key and all its values, use `DELETE /api/v1/translations/keys/{id}`.

#### POST /api/v1/translations/import

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

The response shape is `ImportTranslationsResult` (`Translation.Application/Commands/ImportTranslationsCommand.cs`),
serialized as camelCase — there is no `imported`/`updated`/`errors` shape. `totalKeys` is the
number of entries in the request's `translations` dictionary; `createdKeys` is how many of those
did not already exist as a key; `updatedValues` is how many translation rows were written this
call (every processed entry counts here, whether it created a new value or overwrote an existing
one — see `ImportTranslationsCommandHandler`):

```json
{
  "totalKeys": 3,
  "createdKeys": 3,
  "updatedValues": 3,
  "message": "3 translations imported, 3 new keys created"
}
```

**Per-key tenant overrides in the same file:** any key can carry its own `#tenantId#` prefix to
scope just that entry to one tenant, regardless of the request's top-level `tenantId`. This lets
one uploaded JSON file mix global keys and several tenants' overrides in a single import:

```json
{
  "language": "en",
  "tenantId": null,
  "category": "General",
  "translations": {
    "welcome.message": "Welcome to our app",
    "#acme#welcome.message": "Welcome to Acme Corp",
    "#globex#welcome.message": "Welcome to Globex Inc"
  }
}
```

- `ImportTranslationsCommandHandler` (`Translation.Application/Handlers/Translation/`) reads the
  substring between the first two `#` characters as `keyTenantId`; the effective tenant for that
  row is `keyTenantId ?? request.TenantId` (falls back to the request's top-level `tenantId`, then
  to global if that is also `null`). The `#tenantId#` prefix stays part of the stored key.
- The admin "Import Translations" dialog (`apps/admin/src/app/features/translation/translations/import-dialog/`)
  previews every distinct tenant it detects in the selected file (scanning for `#tenantId#` prefixes)
  before the import request is sent, so an admin can confirm which tenants a file will affect.
- Frontend authors don't need to add anything extra to opt out of tenant scoping — a plain key with
  no `#...#` prefix is always imported as either global or `request.TenantId`, exactly as before.

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
  private apiUrl = "http://localhost:5006/api/v1/translations";

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
  tenantId: string | null;
  translations: { [key: string]: string };
  cachedAt: string;
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
    string? TenantId,
    Dictionary<string, string> Translations,
    string CachedAt
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

        var url = $"/api/v1/translations/{language}";
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

- Cache key: `translations:{language}:{tenantId ?? "global"}:{category ?? "all"}` — exact format read/written in `GetTranslationsQueryHandler.cs`, which still injects `IDistributedCache` directly (not the shared `ICacheService`) for its own get/set. There is no `MicroservicesApp:` prefix and no separate cache-clear endpoint; invalidation happens as a side effect of the mutating command handlers below.
- Expiration: 1 hour (`AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)`), not 5 minutes.
- Invalidation runs through `ITranslationCacheInvalidator` (`Translation.Application/Interfaces/`, implemented by `TranslationCacheInvalidator` in `Translation.Infrastructure/Services/`, which *does* use the shared `ICacheService`) — called by `ImportTranslationsCommandHandler`, `SetTranslationCommandHandler`, `DeleteTranslationValueCommandHandler`, `DeleteTranslationKeyCommandHandler`, and `ToggleTranslationKeyArchivedStatusCommandHandler` after each write.
- `CreateTranslationKeyCommandHandler`/`UpdateTranslationKeyCommandHandler` correctly do **not** invalidate anything — they only touch key metadata, never a `TranslationValue` row, so nothing served by `GetTranslationsQuery` changes.
- **Global writes flush every tenant, not just the `global` bucket:** `GetTranslationsQueryHandler` caches the *merged* result (global value with any tenant override applied on top) under the requesting tenant's own key. So when a write's effective `tenantId` is `null` (a global value changed), any tenant that has no override for that key was serving the old global value from its own cached entry — clearing only `translations:{language}:global:*` left every such tenant stale for up to the full 1-hour TTL. `ITranslationCacheInvalidator.InvalidateAsync` handles this for a `null` tenantId by (1) directly removing its own `translations:{language}:global:all` and `translations:{language}:global:{category}` keys via `ICacheService.RemoveAsync` — a guaranteed, non-pattern delete — and then (2) calling `ICacheService.RemoveByPatternAsync($"translations:{language}:*", ...)` as a best-effort pass to also flush every *other* tenant's cached merged response. Step (1) exists because step (2) alone was found to be unreliable: `RemoveByPatternAsync` needs `IConnectionMultiplexer` (registered by `AddCacheService` only when `Redis:Enabled = true`) and a live `SCAN` match at call time — under the in-memory fallback, or when `GetServers()` finds no connected server, or when the `SCAN` simply misses, it silently no-ops (logged, no exception). A mixed import batch (plain global keys + `#tenantId#`-prefixed keys) was observed clearing the tenant-scoped bucket (via the guaranteed direct delete in the non-null branch below) while leaving `global` stale (relying solely on the pattern flush) — see `Dotnet.instructions.md` pitfall #28. A non-null `tenantId` still only removes that one tenant's `:all`/`:{category}` keys via direct `RemoveAsync`, since a tenant override can't affect any other tenant's cache.
- `ImportTranslationsCommandHandler` lets each imported key carry its own `#tenantId#` prefix (see the Import endpoint section above), so a single batch can touch several different tenants' rows even when the command's own `TenantId` is null — it tracks every distinct effective tenant id seen during the loop (`null` included) and calls `InvalidateAsync` once per distinct id.

---

## Troubleshooting

### Issue: Translations Not Updating

**Symptoms:** Changes to translations not reflected in frontend

**Causes:**

1. Redis cache not invalidated by the write path that changed the data (see the per-tenant invalidation pitfall in the Caching section above — a common cause when the change came from `Import`)
2. Frontend's in-memory `TranslationService` signal cache not refreshed (needs a full page reload or language switch — it does not auto-refetch after an admin mutation; see `MicroservicesArchitecture-Web/Doc/TRANSLATION_SYSTEM_GUIDE.md`)
3. Wrong tenant ID in request

**Solutions:**

```bash
# Clear the whole Translation service Redis cache (there is no per-service FLUSHDB —
# only do this if the service has its own Redis DB/instance, not a shared one)
redis-cli FLUSHDB

# Or delete the specific key manually
redis-cli DEL "translations:en:global:all"
redis-cli DEL "translations:en:tenant-123:all"

# Frontend: reload the page (or switch language) to force the resolver
# to call getTranslations() again and repopulate the signal cache
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
GET /api/v1/translations/keys?searchTerm=welcome.message&tenantId=tenant-123
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
GET /api/v1/user/profile
Authorization: Bearer {token}

# Check token expiration
# JWT tokens expire after AccessTokenExpirationMinutes

# Login again to get fresh token
POST /api/v1/auth/login
```

### Issue: 500 Error When Re-Adding Deleted Translation Key

**Symptoms:** 500 error when creating a key with the same name after deletion

**Cause:** Soft-delete keeps records with `IsArchived = true`. The unique index needs filtering.

**Solution:** The real index is a composite unique index on `(Key, TenantId)`, filtered to
non-archived rows (`TranslationDbContext.OnModelCreating`, `Translation.Infrastructure/Persistence/`)
— not a single-column index on `Key` alone (a single-column unique index on `Key` would incorrectly
prevent a tenant-owned key and a global key from ever sharing the same key string):

```sql
CREATE UNIQUE INDEX "IX_TranslationKeys_Key_TenantId" ON "TranslationKeys" ("Key", "TenantId")
WHERE "IsArchived" = false;
```

You can now delete and re-create keys with the same name (within the same tenant scope) without errors.

---

## Performance Considerations

### Database Indexes

```sql
-- Already created by EF Core migrations (see TranslationDbContext.OnModelCreating)
CREATE UNIQUE INDEX IX_TranslationKeys_Key_TenantId ON TranslationKeys(Key, TenantId) WHERE "IsArchived" = false;
CREATE INDEX IX_TranslationKeys_Category ON TranslationKeys(Category);
CREATE INDEX IX_TranslationKeys_IsActive ON TranslationKeys(IsActive);
CREATE INDEX IX_TranslationKeys_TenantId ON TranslationKeys(TenantId);
CREATE UNIQUE INDEX IX_TranslationValues_Key_Lang_Tenant ON TranslationValues(TranslationKeyId, Language, TenantId);
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
2. **Cache Invalidation**: Automatic on translation updates — per-tenant, per-language, per-category (see the Caching section above)
3. **Cache Warmup**: Pre-load common languages on startup
4. **TTL**: 1 hour (`AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)` in `GetTranslationsQueryHandler.cs`) — not configurable via `MultiTenancy:CacheExpirationMinutes`, that setting is unrelated (it controls the Tenant Service config cache, not this cache)

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

**Last Updated:** August 13, 2026  
**Version:** 1.3  
**Status:** ✅ Production Ready | ✅ Tests: 45/45 Passing
