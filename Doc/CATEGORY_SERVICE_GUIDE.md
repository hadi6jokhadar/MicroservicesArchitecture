# Category Service Guide

**Purpose:** Complete reference for the Category microservice — architecture, data model, API endpoints, CQRS handlers, caching, File Manager integration, and configuration.

**Service Port:** `5007`  
**Last Updated:** May 19, 2026

---

## 📋 Table of Contents

1. [Overview](#-overview)
2. [Project Structure](#-project-structure)
3. [Data Model](#-data-model)
4. [Hierarchy System](#-hierarchy-system-materialized-path)
5. [API Endpoints](#-api-endpoints)
6. [Commands and Queries (CQRS)](#-commands-and-queries-cqrs)
7. [Validation Rules](#-validation-rules)
8. [Caching Strategy](#-caching-strategy)
9. [File Manager Integration](#-file-manager-integration)
10. [Multi-Tenancy](#-multi-tenancy)
11. [Configuration Reference](#-configuration-reference)
12. [Integration Tests](#-integration-tests)
13. [Troubleshooting](#-troubleshooting)

---

## 🏗️ Overview

The Category Service is a platform microservice that provides a **hierarchical category tree** system used by domain apps (e.g., Nasheed). It supports:

- Unlimited depth tree structure using a **materialized path** pattern
- Full CRUD with subtree-aware **move** operation
- **Localized names** stored as JSONB (`{ "en": "Electronics", "ar": "إلكترونيات" }`)
- **Schema-less attributes** (JSONB) per node
- **Optional icon and banner images** via File Manager
- **Redis caching** with pattern-based cache invalidation
- **Database-per-tenant** via `OptionalTenantAttribute` — works with or without `x-tenant-id`
- **Admin endpoints** that bypass tenant context and query the global database

---

## 📁 Project Structure

```
src/Services/Category/
├── Category.API/
│   ├── Program.cs                      # Service bootstrap
│   ├── Endpoints/CategoryEndpoints.cs  # Minimal API route registration
│   ├── Handlers/CategoryApiHandlers.cs # Request → MediatR dispatch
│   └── Filters/ValidationFilter.cs     # FluentValidation endpoint filter
│
├── Category.Application/
│   ├── Commands/CategoryCommands.cs    # MediatR command records
│   ├── Queries/CategoryQueries.cs      # MediatR query records
│   ├── DTOs/CategoryDto.cs             # Response DTO + tree builder
│   ├── DTOs/PaginatedList.cs           # Generic pagination wrapper
│   ├── Validators/CategoryValidators.cs
│   ├── Helpers/CategoryFileManagerHelper.cs  # Batch file enrichment
│   └── Handlers/
│       ├── CreateCategory/
│       ├── UpdateCategory/
│       ├── DeleteCategory/
│       ├── MoveCategory/
│       ├── GetCategoryById/
│       ├── GetCategoryList/
│       └── GetCategoryTree/
│
├── Category.Domain/
│   ├── Entities/CategoryEntity.cs
│   └── Interfaces/ICategoryRepository.cs
│
└── Category.Infrastructure/
    ├── Extensions/InfrastructureServiceExtensions.cs
    └── Persistence/
        ├── CategoryDbContext.cs
        ├── CategoryDbContextFactory.cs
        ├── Configurations/CategoryEntityConfiguration.cs
        └── Repositories/CategoryRepository.cs
```

---

## 🗄️ Data Model

### `CategoryEntity` (table: `categories`)

| Column              | Type            | Constraints               | Description                                         |
| ------------------- | --------------- | ------------------------- | --------------------------------------------------- |
| `id`                | `int`           | PK, auto-increment        | Inherited from `BaseEntity`                         |
| `parent_id`         | `int?`          | FK → `categories.id`      | `null` means root node                              |
| `path`              | `varchar(1000)` | NOT NULL, default `"/"`   | Materialized path built from `uri` segments         |
| `depth`             | `int`           | NOT NULL, default `0`     | Zero-based depth; root = 0                          |
| `slug`              | `varchar(200)`  | NOT NULL                  | URL-friendly identifier; unique per tenant          |
| `uri`               | `varchar(300)`  | NOT NULL                  | URI segment used to build the materialized path     |
| `icon_file_id`      | `int?`          | nullable                  | File ID of the icon image in File Manager           |
| `image_file_id`     | `int?`          | nullable                  | File ID of the banner/cover image in File Manager   |
| `icon_name`         | `varchar(200)?` | nullable                  | CSS class or icon font name                         |
| `name_translations` | `jsonb`         | NOT NULL                  | `{ "en": "...", "ar": "..." }` — `LocalizedMapping` |
| `attributes`        | `jsonb`         | NOT NULL, default `{}`    | Schema-less key-value bag                           |
| `is_archived`       | `bool`          | NOT NULL, default `false` | Soft delete flag (inherited from `BaseEntity`)      |
| `status`            | `int`           | NOT NULL                  | Inherited from `BaseEntity`                         |
| `created`           | `timestamp`     | NOT NULL                  | Inherited from `BaseEntity`                         |
| `last_modified`     | `timestamp?`    | nullable                  | Inherited from `BaseEntity`                         |
| `created_by`        | `string?`       | nullable                  | Inherited from `BaseEntity`                         |
| `last_modified_by`  | `string?`       | nullable                  | Inherited from `BaseEntity`                         |

### `CategoryDto` (response shape)

```json
{
  "id": 3,
  "slug": "phones",
  "uri": "phones",
  "parentId": 1,
  "path": "/electronics/phones/",
  "depth": 1,
  "iconFileId": 42,
  "imageFileId": 43,
  "iconName": "fa-mobile",
  "iconFile": { "id": 42, "url": "...", ... },
  "imageFile": { "id": 43, "url": "...", ... },
  "nameTranslations": { "en": "Phones", "ar": "هواتف" },
  "attributes": { "featured": true },
  "children": [...],
  "isArchived": false,
  "status": 0,
  "created": "2026-05-10T12:00:00Z",
  "lastModified": null
}
```

`children` is only populated in tree responses. In list/single responses it is always `[]`.

### `PaginatedList<CategoryDto>` (list response shape)

```json
{
  "items": [...],
  "totalCount": 50,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 5,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

---

## 🌳 Hierarchy System (Materialized Path)

The service stores the full ancestry path on each node, which allows efficient subtree queries without recursive SQL.

### How paths are built

Paths are constructed from `uri` segments:

```
Root node (parentId = null):    path = "/{uri}/"
Child node:                     path = "{parent.path}{uri}/"
```

**Example tree:**

```
Electronics   → path = "/electronics/"        depth = 0
  └─ Phones   → path = "/electronics/phones/" depth = 1
       └─ Samsung → path = "/electronics/phones/samsung/" depth = 2
```

### Two-step create

On `CreateCategory`, the path is built in two steps:

1. Entity is persisted first (to get the auto-incremented `id`).
2. `RecalculatePath()` is called, then `UpdateAsync` persists the final path.

### Move operation

`MoveCategory` updates the moved node and propagates changes to **all descendants** by replacing the old path prefix with the new one:

```
descendant.newPath = descendant.path.Replace(oldPath, newPath)
```

A move into a descendant of the moved node (circular move) is blocked:

```
if (newParent.Path.StartsWith(entity.Path)) → throw BadRequestException
```

---

## 🌐 API Endpoints

### Base URL: `http://localhost:5007`

All tenant endpoints require `Authorization: Bearer {token}` and optionally `x-tenant-id: {tenantId}`.  
When `x-tenant-id` is omitted the global fallback database is used (`OptionalTenantAttribute`).

---

### Tenant Endpoints — `/api/categories`

#### `POST /api/categories`

Creates a new category node.

**Required role:** Any authenticated user  
**Validation filter:** `CreateCategoryCommandValidator`

**Request body:**

```json
{
  "slug": "electronics",
  "uri": "electronics",
  "nameTranslations": { "en": "Electronics", "ar": "إلكترونيات" },
  "parentId": null,
  "iconFileId": null,
  "imageFileId": null,
  "iconName": "fa-laptop",
  "attributes": { "featured": true }
}
```

**Responses:**
| Status | Description |
|--------|-------------|
| `201 Created` | Category created, returns `CategoryDto` |
| `400 Bad Request` | Validation failure |
| `409 Conflict` | `slug` or `uri` already in use |

---

#### `GET /api/categories`

Returns a paginated flat list of categories (non-archived).

**Query parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `textFilter` | `string?` | `null` | Filter by `slug` contains |
| `pageNumber` | `int` | `1` | Page number |
| `pageSize` | `int` | `10` | Items per page |

**Response:** `PaginatedList<CategoryDto>` — `200 OK`

**Cache key:** `categories:list:{tenantKey}:p{page}:s{size}:f{filter}` — TTL 10 minutes

---

#### `GET /api/categories/tree`

Returns the full category tree for the tenant (nested `children` populated).

**Response:** `List<CategoryDto>` (root nodes with recursive children) — `200 OK`

**Cache key:** `categories:tree:full:{tenantKey}` — TTL 30 minutes

---

#### `GET /api/categories/{id}`

Returns a single category by ID.

**Response:** `CategoryDto` — `200 OK` | `404 Not Found`

**Cache key:** `categories:id:{id}:{tenantKey}` — TTL 10 minutes

---

#### `PUT /api/categories/{id}`

Updates an existing category's fields. All fields are optional (partial update).

**Validation filter:** `UpdateCategoryCommandValidator`

**Request body:**

```json
{
  "slug": "updated-slug",
  "uri": "updated-uri",
  "nameTranslations": { "en": "Updated Name" },
  "iconFileId": 10,
  "imageFileId": 11,
  "iconName": "fa-star",
  "attributes": {}
}
```

**Responses:**
| Status | Description |
|--------|-------------|
| `200 OK` | Returns updated `CategoryDto` |
| `400 Bad Request` | Validation failure |
| `404 Not Found` | Category not found |

---

#### `PUT /api/categories/{id}/move`

Moves a category to a new parent. Pass `newParentId: null` to promote to root.

**Validation filter:** `MoveCategoryCommandValidator`

**Request body:**

```json
{ "newParentId": 5 }
```

**Responses:**
| Status | Description |
|--------|-------------|
| `200 OK` | Returns updated `CategoryDto` |
| `400 Bad Request` | Circular move detected, or validation failure |
| `404 Not Found` | Category or target parent not found |

---

#### `DELETE /api/categories/{id}`

Soft-deletes a category (sets `is_archived = true`).

**Responses:**
| Status | Description |
|--------|-------------|
| `204 No Content` | Successfully archived |
| `404 Not Found` | Category not found |

---

### Admin Endpoints — `/api/admin/categories`

Admin endpoints bypass tenant context (`BypassTenantAttribute`) and always use the global database.

**Required role:** `Admin` or `SuperAdmin`

| Method | Path                         | Description                           |
| ------ | ---------------------------- | ------------------------------------- |
| `GET`  | `/api/admin/categories`      | Paginated list across global database |
| `GET`  | `/api/admin/categories/tree` | Full tree from global database        |

---

## 📬 Commands and Queries (CQRS)

### Commands

| Command                 | Handler                        | Description                                                                       |
| ----------------------- | ------------------------------ | --------------------------------------------------------------------------------- |
| `CreateCategoryCommand` | `CreateCategoryCommandHandler` | Creates a new node, validates uniqueness of slug/uri, builds path, enriches files |
| `UpdateCategoryCommand` | `UpdateCategoryCommandHandler` | Partial update; clears related cache keys                                         |
| `MoveCategoryCommand`   | `MoveCategoryCommandHandler`   | Moves node + propagates path/depth to all descendants                             |
| `DeleteCategoryCommand` | `DeleteCategoryCommandHandler` | Soft-deletes via `is_archived = true`                                             |

### Queries

| Query                          | Handler                       | Description                                                       |
| ------------------------------ | ----------------------------- | ----------------------------------------------------------------- |
| `GetCategoryByIdQuery(int Id)` | `GetCategoryByIdQueryHandler` | Single category with file enrichment; cached 10 min               |
| `GetCategoryListQuery`         | `GetCategoryListQueryHandler` | Paginated flat list with file enrichment; cached 10 min           |
| `GetCategoryTreeQuery`         | `GetCategoryTreeQueryHandler` | Full tree with nested children and file enrichment; cached 30 min |

---

## ✅ Validation Rules

### `CreateCategoryCommand`

| Field              | Rules                                                        |
| ------------------ | ------------------------------------------------------------ |
| `Slug`             | Required, max 200 chars, regex: `^[a-z0-9]+(?:-[a-z0-9]+)*$` |
| `Uri`              | Required, max 300 chars, regex: `^[a-z0-9]+(?:-[a-z0-9]+)*$` |
| `NameTranslations` | Required, must have at least one entry                       |
| `ParentId`         | When provided: must be `> 0`                                 |

### `UpdateCategoryCommand`

| Field  | Rules                                              |
| ------ | -------------------------------------------------- |
| `Id`   | Required, `> 0`                                    |
| `Slug` | When provided: max 200 chars, same regex as create |
| `Uri`  | When provided: max 300 chars, same regex as create |

### `MoveCategoryCommand`

| Field               | Rules                               |
| ------------------- | ----------------------------------- |
| `Id`                | Required, `> 0`                     |
| `NewParentId`       | When provided: must be `> 0`        |
| `Id != NewParentId` | A category cannot be its own parent |

---

## ⚡ Caching Strategy

All read operations use **Redis** with tenant-scoped cache keys.

| Operation | Cache Key Pattern                                              | TTL    |
| --------- | -------------------------------------------------------------- | ------ |
| Get tree  | `categories:tree:full:{tenantId\|global}`                      | 30 min |
| Get list  | `categories:list:{tenantId\|global}:p{page}:s{size}:f{filter}` | 10 min |
| Get by ID | `categories:id:{id}:{tenantId\|global}`                        | 10 min |

### Cache invalidation

Any write operation (create, update, move, delete) removes affected cache entries by pattern:

```
categories:tree*         ← cleared on all mutations
categories:list*         ← cleared on all mutations
categories:id:{id}       ← cleared on update/delete of specific item
```

Pattern-based removal is performed with `ICacheService.RemoveByPatternAsync(...)`.

---

## 📎 File Manager Integration

Categories support optional icon and banner images. Files are resolved via a **service-to-service** call to the File Manager service on every read, using a batch request to avoid N+1 queries.

### `CategoryFileManagerHelper`

Two methods are available:

| Method                                 | Usage                                     |
| -------------------------------------- | ----------------------------------------- |
| `EnrichCategoryWithFilesAsync(dto)`    | Single category — called in `GetById`     |
| `EnrichCategoriesWithFilesAsync(dtos)` | Batch — called in `GetList` and `GetTree` |

Both collect all `iconFileId` and `imageFileId` values from the DTOs, deduplicating them, then issue one `GetFilesByIdsAsync` call to File Manager and map results back.

### Registration

```csharp
// InfrastructureServiceExtensions.cs
services.AddFileManagerServiceClient(configuration, "CategoryService");
services.AddScoped<CategoryFileManagerHelper>();
```

### Configuration

```json
"Services": {
  "FileManagerService": {
    "BaseUrl": "http://localhost:5005"
  }
}
```

---

## 🏢 Multi-Tenancy

The Category service uses **optional tenant context** (`OptionalTenantAttribute`):

- **With `x-tenant-id` header:** resolves tenant database and uses it.
- **Without `x-tenant-id` header:** falls back to the global `DatabaseSettings:ConnectionString`.

Admin endpoints use `BypassTenantAttribute`, which always targets the global database regardless of any header.

The cache key uses `_tenantContext.TenantId ?? "global"` to isolate cache entries per tenant.

See `DATABASE_PER_TENANT_ARCHITECTURE.md` and `BYPASS_TENANT_ENDPOINTS_GUIDE.md` for the full pattern.

---

## ⚙️ Configuration Reference

### `appsettings.json` (key sections)

```json
{
  "Urls": "http://localhost:5007",
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=global;..."
  },
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "http://localhost:5002"
  },
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "MicroservicesApp:"
  },
  "Services": {
    "TenantService": { "BaseUrl": "http://localhost:5002" },
    "FileManagerService": { "BaseUrl": "http://localhost:5005" }
  },
  "ServiceCommunication": {
    "ServiceName": "CategoryService",
    "SharedSecret": "...",
    "AllowedServices": [
      "IdentityService",
      "NotificationService",
      "TenantService",
      "AiService"
    ]
  },
  "RateLimiting": {
    "Global": { "PermitLimit": 10000, "WindowMinutes": 1 },
    "PerIP": { "PermitLimit": 200, "WindowMinutes": 1 },
    "PerTenant": { "PermitLimit": 2000, "WindowMinutes": 1 },
    "PerUser": { "PermitLimit": 500, "WindowMinutes": 1 }
  }
}
```

### Rate limiting tiers

| Policy     | Limit      | Window |
| ---------- | ---------- | ------ |
| Global     | 10,000 req | 1 min  |
| Per IP     | 200 req    | 1 min  |
| Per Tenant | 2,000 req  | 1 min  |
| Per User   | 500 req    | 1 min  |

Exceeding the limit returns `429 Too Many Requests` with a `retryAfter` seconds field.

---

## 🧪 Integration Tests

Tests are in `Category.API.Tests/`. They follow the standard `CustomWebApplicationFactory` + `IntegrationTestBase` pattern shared across all services.

See `SERVICE_INTEGRATION_TEST_GUIDE.md` for the full recipe.

Key test file: `Endpoints/CategoryEndpointsTests.cs`

Test infrastructure:

- `CustomWebApplicationFactory.cs` — replaces DB with SQLite in-memory and replaces file manager client with a stub
- `IntegrationTestBase.cs` — shared setup/teardown
- `SequentialCollectionDefinition.cs` — ensures tests run sequentially within a collection

---

## 🔧 Troubleshooting

### `409 Conflict` on create

`slug` or `uri` already exists in the tenant database. Use a unique value. Both fields are validated before any DB write via `SlugExistsAsync` / `UriExistsAsync`.

### Tree is stale after mutations

Cache TTL is 30 minutes for the tree. After any write, `categories:tree*` is cleared immediately. If stale data persists, verify Redis is reachable and that `ICacheService.RemoveByPatternAsync` is executing without error.

### `iconFile` / `imageFile` always null

Verify the File Manager service is running on `http://localhost:5005` and that `ServiceCommunication.SharedSecret` matches between Category and File Manager. Check logs for `Failed to fetch files for Category {CategoryId}`.

### Move returns `400 Bad Request` with "Cannot move into own descendant"

The target `newParentId` is a descendant of the category being moved. Choose a different target or move to root by passing `newParentId: null`.

### Path not updating for descendants after move

`MoveCategoryCommandHandler` iterates all subtree nodes returned by `GetSubtreeAsync`. If a descendant's path shows the old prefix, confirm that `GetSubtreeAsync` is not filtering out archived nodes that are part of the subtree.
