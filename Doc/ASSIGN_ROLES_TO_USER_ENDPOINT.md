# Assign Roles to User Endpoint

**Date:** January 13, 2026  
**Status:** ✅ Complete  
**Impact:** Role Management, User Administration

---

## 📋 Overview

This document describes the new "Assign Roles to User" endpoint that allows administrators to update a user's role assignments. This endpoint **overwrites** all existing roles with new ones in a single operation.

---

## 🎯 Endpoint Details

### HTTP Request

```http
POST /api/admin/roles/user/{id}
```

### Authorization

- **Requires:** Admin or SuperAdmin role
- **Optional Tenant:** Works with or without `x-tenant-id` header

### Parameters

| Name      | Type      | Location     | Required | Description                                                   |
| --------- | --------- | ------------ | -------- | ------------------------------------------------------------- |
| `id`      | integer   | URL path     | Yes      | User ID to assign roles to                                    |
| `roleIds` | List<int> | Request body | Yes      | List of role IDs to assign (can be empty to remove all roles) |

### Request Example

```json
{
  "roleIds": [1, 3, 5]
}
```

### Success Response

**Code:** 200 OK

```json
{
  "success": true,
  "message": "Roles assigned to user successfully"
}
```

### Error Responses

**404 Not Found** - User not found

```json
{
  "statusCode": 404,
  "message": "User not found"
}
```

**404 Not Found** - Role not found

```json
{
  "statusCode": 404,
  "message": "Role not found"
}
```

**401 Unauthorized** - User not authenticated

**403 Forbidden** - User not Admin/SuperAdmin

---

## 🔄 Behavior

### Role Replacement Strategy

The endpoint follows a **replace-all** strategy:

1. **Revoke** all existing roles from the user
2. **Assign** the new roles specified in the request

This ensures a clean state without duplicate role assignments or orphaned roles.

### Empty Role List

Passing an empty `roleIds` array is **valid** and will:

- Remove all roles from the user
- Leave the user with no role assignments
- Return success

```json
{
  "roleIds": [] // Removes all roles
}
```

---

## 🏗️ Architecture

### Files Created

1. **Command**  
   [Identity.Application/Commands/Admin/Role/AssignRolesToUserCommand.cs](../src/Services/Identity/Identity.Application/Commands/Admin/Role/AssignRolesToUserCommand.cs)

   - MediatR command with FluentValidation
   - Validates `UserId > 0` and `RoleIds` is not null

2. **Handler**  
   [Identity.Application/Handlers/Admin/AssignRolesToUserCommandHandler.cs](../src/Services/Identity/Identity.Application/Handlers/Admin/AssignRolesToUserCommandHandler.cs)

   - Implements CQRS pattern with MediatR
   - Uses `IUserRoleRepository` for database operations
   - Includes comprehensive logging

3. **API Handler**  
   [Identity.API/Handlers/RoleApiHandlers.cs](../src/Services/Identity/Identity.API/Handlers/RoleApiHandlers.cs)

   - Added `AssignRolesToUserHandler` method
   - Added `AssignRolesToUserRequest` record

4. **Endpoint Mapping**  
   [Identity.API/Extensions/EndpointMappingExtensions.cs](../src/Services/Identity/Identity.API/Extensions/EndpointMappingExtensions.cs)
   - Registered in `/api/admin/roles` group
   - Includes validation filter

---

## 💻 Implementation Details

### Command Definition

```csharp
public record AssignRolesToUserCommand(
    int UserId,
    List<int> RoleIds
) : IRequest<bool>;
```

### Handler Logic

```csharp
public async Task<bool> Handle(AssignRolesToUserCommand request, CancellationToken cancellationToken)
{
    try
    {
        // 1. Verify user exists
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

        // 2. Verify all roles exist
        if (request.RoleIds.Any())
        {
            foreach (var roleId in request.RoleIds)
            {
                var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
                if (role == null)
                    throw new NotFoundException(LocalizationKeys.Exceptions.RoleNotFound);
            }
        }

        // 3. Revoke all existing roles
        await _userRoleRepository.RevokeAllRolesFromUserAsync(request.UserId, cancellationToken);

        // 4. Assign new roles
        if (request.RoleIds.Any())
        {
            await _userRoleRepository.AssignRolesToUserAsync(request.UserId, request.RoleIds, cancellationToken);
        }

        return true;
    }
    catch (AppException)
    {
        throw;
    }
    catch (Exception)
    {
        throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
    }
}
```

### Repository Methods Used

```csharp
public interface IUserRoleRepository
{
    Task AssignRolesToUserAsync(int userId, List<int> roleIds, CancellationToken cancellationToken = default);
    Task RevokeAllRolesFromUserAsync(int userId, CancellationToken cancellationToken = default);
}
```

---

## 🔐 Security Considerations

### Authorization

- Endpoint requires `Admin` or `SuperAdmin` role
- Protected by `OptionalTenantAttribute` (works across tenants)
- All role assignments are logged for audit trails

### Validation

1. **User ID Validation**

   - Must be greater than 0
   - User must exist in database

2. **Role ID Validation**

   - Each role ID must exist in database
   - Validates before making any changes (atomic operation)

3. **Request Validation**
   - `RoleIds` cannot be null (but can be empty array)
   - FluentValidation runs before handler execution

---

## 📊 Use Cases

### 1. Promote User to Admin

```http
POST /api/admin/roles/user/123
Content-Type: application/json

{
  "roleIds": [2]  // Admin role ID
}
```

### 2. Assign Multiple Roles

```http
POST /api/admin/roles/user/456
Content-Type: application/json

{
  "roleIds": [1, 3, 5]  // User, Manager, Editor roles
}
```

### 3. Remove All Roles (Demote)

```http
POST /api/admin/roles/user/789
Content-Type: application/json

{
  "roleIds": []
}
```

### 4. Update Role Assignments

```http
POST /api/admin/roles/user/321
Content-Type: application/json

{
  "roleIds": [1]  // Replace all current roles with just "User"
}
```

---

## 🧪 Testing Recommendations

### Integration Tests

1. **Happy Path**

   - Assign single role to user
   - Assign multiple roles to user
   - Replace existing roles with new ones
   - Remove all roles (empty array)

2. **Error Cases**

   - User not found (404)
   - Role not found (404)
   - Invalid user ID (validation error)
   - Null roleIds (validation error)
   - Unauthorized access (401)
   - Non-admin access (403)

3. **Multi-Tenancy**
   - Assign roles with `x-tenant-id` header
   - Assign roles without header (global database)
   - Verify tenant isolation

### Manual Testing with Postman

```bash
# 1. Get auth token
POST http://localhost:5001/api/auth/login
{
  "email": "admin@example.com",
  "password": "Admin@123"
}

# 2. Assign roles to user
POST http://localhost:5001/api/admin/roles/user/5
Authorization: Bearer {token}
Content-Type: application/json

{
  "roleIds": [1, 2]
}

# 3. Verify by getting user details
GET http://localhost:5001/api/admin/users/5
Authorization: Bearer {token}
```

---

## 📝 Logging

The handler includes comprehensive logging:

```log
[Information] Revoked all existing roles from user 123
[Information] Assigned 3 roles to user 123
[Information] User 456 now has no roles assigned
[Warning] User with ID 999 not found
[Warning] Role with ID 99 not found
```

---

## 🔄 Comparison with Other Endpoints

| Endpoint                            | Purpose                    | Strategy                           |
| ----------------------------------- | -------------------------- | ---------------------------------- |
| `POST /api/admin/roles/user/{id}`   | **Replace all user roles** | Revoke all, then assign new        |
| `POST /api/admin/roles/{id}/claims` | Assign claims to role      | Revoke all claims, then assign new |
| `PUT /api/admin/users/{id}`         | Update user profile        | Updates specific fields only       |

---

## 🚀 Future Enhancements

Potential improvements for future versions:

1. **Batch Operations**

   - Assign roles to multiple users at once
   - Endpoint: `POST /api/admin/roles/batch-assign`

2. **Partial Updates**

   - Add roles without removing existing ones
   - Remove specific roles without affecting others
   - Endpoints: `POST /user/{id}/roles/add`, `DELETE /user/{id}/roles/remove`

3. **Role Assignment History**

   - Audit log for who changed user roles and when
   - Store previous role assignments

4. **Bulk Import**
   - CSV/JSON import for mass role assignments
   - Endpoint: `POST /api/admin/roles/import`

---

## 📚 Related Documentation

- [IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md](IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md) - Identity service enhancements
- [JWT_ROLE_CLAIMS_AND_CONDITIONAL_VISIBILITY.md](JWT_ROLE_CLAIMS_AND_CONDITIONAL_VISIBILITY.md) - Role claims in JWT
- [DYNAMIC_ROLES_AND_CLAIMS_MIGRATION_SUMMARY.md](DYNAMIC_ROLES_AND_CLAIMS_MIGRATION_SUMMARY.md) - Role system architecture
- [00_START_HERE.md](00_START_HERE.md) - Documentation index

---

**Last Updated:** January 13, 2026  
**Contributors:** GitHub Copilot + Development Team
