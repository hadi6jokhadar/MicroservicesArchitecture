# 🌍 Translation Service - Quick Reference

**Version:** 1.1  
**Port:** 5006  
**Database:** Global (single DB for all tenants)  
**Test Status:** ✅ 45/45 Tests Passing (100%)

---

## 🎯 Quick Start

### Get Translations (Public - No Auth)

```http
# Global translations
GET http://localhost:5006/api/translations/en

# Tenant-specific translations
GET http://localhost:5006/api/translations/en
x-tenant-id: tenant-123

# Filter by category
GET http://localhost:5006/api/translations/en?category=Validation
```

###Create Translation Key (Admin)

```http
POST http://localhost:5006/api/translations/keys
Authorization: Bearer {admin-jwt-token}
Content-Type: application/json

{
  "key": "welcome.message",
  "category": "General",
  "description": "Welcome message on homepage"
}
```

### Set Translation Value (Admin)

```http
# Global translation
POST http://localhost:5006/api/translations
Authorization: Bearer {admin-jwt-token}
Content-Type: application/json

{
  "key": "welcome.message",
  "language": "en",
  "value": "Welcome to our application",
  "category": "General",
  "tenantId": null
}

# Tenant override
POST http://localhost:5006/api/translations
Authorization: Bearer {admin-jwt-token}
Content-Type: application/json

{
  "key": "welcome.message",
  "language": "en",
  "value": "Welcome to Acme Corp",
  "category": "General",
  "tenantId": "tenant-123"
}
```

---

## 📚 Core Concepts

### Global vs Tenant Translations

| Type            | TenantId   | Access        | Priority |
| --------------- | ---------- | ------------- | -------- |
| Global          | `null`     | All tenants   | Default  |
| Tenant Override | `tenant-*` | Single tenant | Override |

### Translation Merge Logic

```
Request with x-tenant-id: tenant-123
├── Query: (TenantId IS NULL OR TenantId = 'tenant-123')
├── Result: { global: {...}, tenant: {...} }
└── Merge: tenant values override global values
```

---

## 🔑 API Endpoints

### Public Endpoints

| Method | Endpoint                       | Auth | Description                   |
| ------ | ------------------------------ | ---- | ----------------------------- |
| GET    | `/api/translations/{language}` | No   | Get translations for language |

**Query Parameters:**

- `category` (optional): Filter by category
- Header: `x-tenant-id` (optional): Tenant-specific overrides

---

### Admin Endpoints (Require Admin Role)

| Method | Endpoint                       | Description                  |
| ------ | ------------------------------ | ---------------------------- |
| GET    | `/api/translations/keys`       | List translation keys        |
| POST   | `/api/translations/keys`       | Create translation key       |
| PUT    | `/api/translations/keys/{id}`  | Update translation key       |
| DELETE | `/api/translations/keys/{id}`  | Delete translation key       |
| POST   | `/api/translations`            | Set translation value        |
| POST   | `/api/translations/import`     | Bulk import translations     |
| DELETE | `/api/translations/{language}` | Delete language translations |

---

## 💻 Integration Examples

### Frontend (TypeScript)

```typescript
// Get translations
async function getTranslations(language: string, tenantId?: string) {
  const headers = tenantId ? { "x-tenant-id": tenantId } : {};
  const response = await fetch(
    `http://localhost:5006/api/translations/${language}`,
    { headers },
  );
  return await response.json();
}

// Usage
const translations = await getTranslations("en", "tenant-123");
console.log(translations.translations["welcome.message"]);
```

### Backend (.NET)

```csharp
// GET translations
public async Task<TranslationsDto> GetTranslationsAsync(
    string language,
    string? tenantId = null)
{
    var client = _httpClientFactory.CreateClient();
    var request = new HttpRequestMessage(
        HttpMethod.Get,
        $"http://localhost:5006/api/translations/{language}"
    );

    if (!string.IsNullOrEmpty(tenantId))
        request.Headers.Add("x-tenant-id", tenantId);

    var response = await client.SendAsync(request);
    response.EnsureSuccessStatusCode();

    return await response.Content.ReadFromJsonAsync<TranslationsDto>();
}
```

---

## 🗄️ Database Schema

### TranslationKeys

```
Id              INT (PK)
Key             VARCHAR(200) UNIQUE - e.g., "welcome.message"
Category        VARCHAR(100)        - e.g., "General"
Description     VARCHAR(500)
IsActive        BOOLEAN
```

### TranslationValues

```
Id                INT (PK)
TranslationKeyId  INT (FK) → TranslationKeys.Id
Language          VARCHAR(10)        - e.g., "en", "ar"
Value             TEXT               - Translation text
TenantId          VARCHAR(450) NULL  - NULL for global, ID for tenant
```

**Unique Constraint:** `(TranslationKeyId, Language, TenantId)`

---

## 🎨 Best Practices

### Naming Conventions

```
✅ Use dot notation:
- error.required
- validation.email.invalid
- navigation.home

❌ Avoid:
- ERROR_REQUIRED
- emailValidationError
```

### Parameter Placeholders

```json
{
  "error.required": "{0} is required",
  "error.maxLength": "{0} cannot exceed {1} characters"
}
```

**Usage:**

```typescript
translate("error.required", "Email"); // "Email is required"
translate("error.maxLength", "Name", "50"); // "Name cannot exceed 50 characters"
```

### Categories

- `General` - Common messages
- `Validation` - Form validation
- `Error` - Error messages
- `Success` - Success messages
- `Navigation` - Menu items
- `Email` - Email templates

---

## ⚙️ Configuration

### Required appsettings.json

```json
{
  "Urls": "http://localhost:5006",
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=translation;..."
  },
  "Jwt": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters-long",
    "Issuer": "IhsanDev",
    "Audience": "MicroservicesApp"
  },
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "http://localhost:5002",
    "JwtMode": "PerTenant"
  },
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379"
  }
}
```

---

## 🔍 Common Scenarios

### Scenario 1: Add New Translation

```bash
# Step 1: Create translation key
POST /api/translations/keys
{
  "key": "app.slogan",
  "category": "General",
  "description": "Company slogan"
}

# Step 2: Add English translation
POST /api/translations
{
  "key": "app.slogan",
  "language": "en",
  "value": "Innovation Made Simple",
  "tenantId": null
}

# Step 3: Add Arabic translation
POST /api/translations
{
  "key": "app.slogan",
  "language": "ar",
  "value": "الابتكار أصبح بسيطاً",
  "tenantId": null
}
```

### Scenario 2: Tenant Customization

```bash
# Step 1: Tenant wants custom welcome message
POST /api/translations
{
  "key": "welcome.message",
  "language": "en",
  "value": "Welcome to Acme Corporation",
  "tenantId": "acme-corp"
}

# Result when acme-corp requests translations:
GET /api/translations/en
x-tenant-id: acme-corp

Response:
{
  "translations": {
    "welcome.message": "Welcome to Acme Corporation"  // Overridden
  }
}

# Other tenants still see global version:
GET /api/translations/en
x-tenant-id: other-tenant

Response:
{
  "translations": {
    "welcome.message": "Welcome"  // Global
  }
}
```

### Scenario 3: Bulk Import

```http
POST /api/translations/import
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "language": "en",
  "tenantId": null,
  "category": "Validation",
  "translations": {
    "error.required": "{0} is required",
    "error.email": "{0} must be a valid email",
    "error.minLength": "{0} must be at least {1} characters",
    "error.maxLength": "{0} cannot exceed {1} characters"
  }
}
```

---

## 🐛 Troubleshooting

### Translations Not Showing

```bash
# Check if translation exists
GET /api/translations/keys?searchTerm=welcome.message

# Verify correct language
GET /api/translations/en  # Not /api/translations/EN

# Check tenant ID matches
GET /api/translations/en
x-tenant-id: tenant-123  # Exact match required
```

### Admin Endpoints Return 401

```bash
# Verify JWT token is valid
GET /api/user/profile
Authorization: Bearer {token}

# Check user has Admin role
Response should include: "role": "Admin" or "SuperAdmin"

# Get new token if expired
POST /api/auth/login
```

### Cache Issues

```bash
# Clear Redis cache
redis-cli FLUSHDB

# Or restart Redis
docker restart redis

# Or disable Redis temporarily
appsettings.json: "Redis:Enabled": false
```

---

## 📊 Response Examples

### Success Response

```json
{
  "language": "en",
  "category": null,
  "tenantId": "tenant-123",
  "translations": {
    "welcome.message": "Welcome to Acme Corp",
    "login.button": "Login",
    "error.required": "{0} is required"
  }
}
```

### Error Response (Validation)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Key": ["Translation key is required"],
    "Language": ["Language is required"]
  }
}
```

### Error Response (Unauthorized)

```json
{
  "error": "Unauthorized",
  "message": "Admin role required for this endpoint"
}
```

---

## 🔗 Related Documentation

- [TRANSLATION_SERVICE_GUIDE.md](TRANSLATION_SERVICE_GUIDE.md) - Complete guide
- [TRANSLATION_SERVICE_TEST_FIX_SUMMARY.md](TRANSLATION_SERVICE_TEST_FIX_SUMMARY.md) - Test infrastructure & fixes
- [TRANSLATION_SERVICE_FINAL_VERIFICATION.md](TRANSLATION_SERVICE_FINAL_VERIFICATION.md) - Design pattern verification
- [LOCALIZATION_GUIDE.md](LOCALIZATION_GUIDE.md) - Application localization
- [MULTI_TENANCY_GUIDE.md](MULTI_TENANCY_GUIDE.md) - Multi-tenancy patterns
- [SHARED_TESTING_FILES.md](SHARED_TESTING_FILES.md) - Testing best practices
- [00_START_HERE.md](00_START_HERE.md) - Architecture overview

---

**Quick Links:**

- Swagger UI: http://localhost:5006/swagger
- Health Check: http://localhost:5006/
- Port: 5006
- Database: `translation` (PostgreSQL)

---

**Last Updated:** January 27, 2026  
**Version:** 1.1  
**Status:** ✅ Production Ready | ✅ Tests: 45/45 Passing
