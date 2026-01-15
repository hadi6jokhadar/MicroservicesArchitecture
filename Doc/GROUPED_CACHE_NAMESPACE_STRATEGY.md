# Grouped Cache Namespace Strategy

**Last Updated:** January 15, 2026  
**Status:** ✅ Production Ready  
**Version:** 1.0

---

## Overview

This document describes the **grouped cache namespace strategy** implemented across the Identity Service to keep Redis clean and organized. Instead of using flat, scattered cache keys, related data is now grouped under hierarchical namespace prefixes.

### Key Benefits

✅ **Cleaner Redis namespace** - All related data organized by logical groups  
✅ **Better management** - Easy to identify and invalidate related cache groups  
✅ **Scalability** - Clear hierarchical structure for future expansions  
✅ **Consistency** - Uniform cache key naming convention across all handlers  
✅ **Reduced fragmentation** - Fewer orphaned keys in Redis

---

## Cache Namespace Structure

### Claims Group: `admin:claims`

All claims-related cache keys follow the pattern `admin:claims:*`

| Cache Key                        | Purpose                         | TTL        |
| -------------------------------- | ------------------------------- | ---------- |
| `admin:claims`                   | All claims (list)               | 30 minutes |
| `admin:claims:{id}`              | Single claim by ID              | 30 minutes |
| `admin:claims:name_{normalized}` | Lookup by normalized claim name | 30 minutes |

**Example:**

```redis
admin:claims                          → List<ClaimDto>
admin:claims:1                        → ClaimDto { Id: 1, Name: "read", ... }
admin:claims:name_READ                → ClaimDto { ... }
```

### Roles Group: `admin:roles`

All roles-related cache keys follow the pattern `admin:roles:*`

| Cache Key                       | Purpose                          | TTL        |
| ------------------------------- | -------------------------------- | ---------- |
| `admin:roles`                   | All roles (list)                 | 30 minutes |
| `admin:roles:{id}`              | Single role by ID                | 30 minutes |
| `admin:roles:name_{normalized}` | Lookup by normalized role name   | 30 minutes |
| `admin:roles:{id}:claims`       | Claims assigned to specific role | 30 minutes |

**Example:**

```redis
admin:roles                           → List<RoleDto>
admin:roles:1                         → RoleDto { Id: 1, Name: "SuperAdmin", ... }
admin:roles:name_SUPERADMIN           → RoleDto { ... }
admin:roles:1:claims                  → List<ClaimDto>
```

---

## Implementation Details

### Query Handlers

**GetClaimsQueryHandler & GetClaimByIdQueryHandler**

- List all claims: Uses `admin:claims` group key
- Get single claim: Uses `admin:claims:{id}` namespaced key
- Automatically invalidated when claims are modified

**GetRolesQueryHandler & GetRoleByIdQueryHandler**

- List all roles: Uses `admin:roles` group key
- Get single role: Uses `admin:roles:{id}` namespaced key
- Automatically invalidated when roles are modified

### Command Handlers - Cache Invalidation

When any claims or roles are modified, all related cache keys in that group are invalidated:

**Claims Operations:**
| Operation | Invalidated Keys |
| ------------------ | ----------------------------------------------------------------- |
| Create Claim | `admin:claims`, `admin:claims:{id}` |
| Update Claim | `admin:claims`, `admin:claims:{id}`, `admin:claims:name_{name}` |
| Delete Claim | `admin:claims`, `admin:claims:{id}`, `admin:claims:name_{name}` |

**Roles Operations:**
| Operation | Invalidated Keys |
| -------------------- | ------------------------------------------------------------------------ |
| Create Role | `admin:roles`, `admin:roles:{id}` |
| Update Role | `admin:roles`, `admin:roles:{id}`, `admin:roles:name_{name}` |
| Delete Role | `admin:roles`, `admin:roles:{id}`, `admin:roles:name_{name}` |
| Assign Claims to Role| `admin:roles`, `admin:roles:{id}`, `admin:roles:{id}:claims` |

---

## Code Examples

### Querying (Cache Hit)

```csharp
// Get all claims from grouped cache
const string groupKey = "admin:claims";
var cachedClaims = await _cacheService.GetAsync<List<ClaimDto>>(groupKey, cancellationToken);
if (cachedClaims != null)
    return cachedClaims;
```

### Querying Individual Item

```csharp
// Get single claim with namespaced key
const string groupKey = "admin:claims";
var cacheKey = $"{groupKey}:{request.Id}";  // "admin:claims:1"
var cachedClaim = await _cacheService.GetAsync<ClaimDto>(cacheKey, cancellationToken);
if (cachedClaim != null)
    return cachedClaim;
```

### Cache Invalidation

```csharp
// When creating a new claim
const string groupKey = "admin:claims";
await _cacheService.RemoveAsync(groupKey, cancellationToken);
await _cacheService.RemoveAsync($"{groupKey}:{claim.Id}", cancellationToken);
```

---

## Affected Files

### Identity Service - Query Handlers

- `Identity.Application/Handlers/Admin/Claim/GetClaimsQueryHandler.cs`
- `Identity.Application/Handlers/Admin/Role/GetRolesQueryHandler.cs`

### Identity Service - Command Handlers

- `Identity.Application/Handlers/Admin/Claim/CreateClaimCommandHandler.cs`
- `Identity.Application/Handlers/Admin/Claim/UpdateClaimCommandHandler.cs`
- `Identity.Application/Handlers/Admin/Claim/DeleteClaimCommandHandler.cs`
- `Identity.Application/Handlers/Admin/Role/CreateRoleCommandHandler.cs`
- `Identity.Application/Handlers/Admin/Role/UpdateRoleCommandHandler.cs`
- `Identity.Application/Handlers/Admin/Role/DeleteRoleCommandHandler.cs`
- `Identity.Application/Handlers/Admin/Role/AssignClaimsToRoleCommandHandler.cs`

---

## Redis Memory Impact

### Before Grouped Caching

- Multiple scattered keys per entity
- Higher cardinality in Redis (more unique keys)
- Harder to correlate related data

### After Grouped Caching

- Organized under hierarchical namespaces
- Lower cardinality with better organization
- Easier bulk invalidation within groups
- Example: Invalidating all claims requires removing only 1 group key

---

## Monitoring & Debugging

### Redis CLI Commands

```bash
# View all claims group keys
redis-cli KEYS "admin:claims*"

# View all roles group keys
redis-cli KEYS "admin:roles*"

# Get a specific key
redis-cli GET "admin:claims:1"

# Clear a specific group
redis-cli DEL "admin:claims"

# Clear all claims-related cache
redis-cli DEL $(redis-cli KEYS "admin:claims*" | tr '\n' ' ')
```

### Logging

Cache operations are logged through the standard `ICacheService` interface:

```csharp
// Set operation
await _cacheService.SetAsync("admin:claims", claimDtos, TimeSpan.FromMinutes(30), cancellationToken);

// Get operation
var cached = await _cacheService.GetAsync<List<ClaimDto>>("admin:claims", cancellationToken);

// Remove operation
await _cacheService.RemoveAsync("admin:claims", cancellationToken);
```

---

## Future Expansions

This grouped namespace strategy can be extended to other services:

```
admin:users               → User management cache
admin:settings            → System settings cache
admin:audit              → Audit log cache
notifications:fcm        → Firebase configuration cache
files:uploads            → File upload metadata cache
```

---

## Related Documentation

- [CACHING_STRATEGY_COMPARISON.md](CACHING_STRATEGY_COMPARISON.md)
- [REDIS_ENABLED_VS_DISABLED_GUIDE.md](REDIS_ENABLED_VS_DISABLED_GUIDE.md)
- [REDIS_CACHE_QUICK_REFERENCE.md](REDIS_CACHE_QUICK_REFERENCE.md)
- [IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md](IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md)
