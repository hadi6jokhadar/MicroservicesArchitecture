# Old Tests Migration Guide - UserRole Enum to Database Roles

**Date:** January 2026  
**Status:** ✅ Migration Complete (Jan 12, 2026) - All 142 Tests Passing  
**Related:** [ROLE_CLAIM_ENDPOINTS_WITH_REDIS_SUMMARY.md](ROLE_CLAIM_ENDPOINTS_WITH_REDIS_SUMMARY.md), [DYNAMIC_ROLES_AND_CLAIMS_MIGRATION_SUMMARY.md](DYNAMIC_ROLES_AND_CLAIMS_MIGRATION_SUMMARY.md), [IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md](IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md)

---

## 📋 Overview

After migrating from enum-based roles to database-driven roles, existing integration tests needed to be updated. **This migration has been completed successfully** with all 142 tests passing.

**Migration Results:**

- ✅ All 142 integration tests passing (100%)
- ✅ SuperAdmin protection tests updated (ForbiddenException)
- ✅ Entity reload implemented in handlers
- ✅ Test helper methods updated for role IDs
- ✅ No test regressions detected

---

## 🎯 What Changed

### Before (Old - Using Enum)

```csharp
// CreateUserCommand with enum
var createCommand = new CreateUserCommand(
    Email: "user@example.com",
    Password: "Pass123!",
    FirstName: "John",
    LastName: "Doe",
    Role: UserRole.User,  // ❌ Obsolete enum
    PhoneNumber: "+1234567890"
);

// User entity had Role property
var user = new User {
    Role = UserRole.Admin  // ❌ Property removed
};

// DTO had Role property
result.Role.Should().Be(UserRole.User);  // ❌ Property removed
```

### After (New - Using Database Roles)

```csharp
// CreateUserCommand with role IDs
var createCommand = new CreateUserCommand(
    Email: "user@example.com",
    Password: "Pass123!",
    FirstName: "John",
    LastName: "Doe",
    RoleIds: new List<int> { 3 },  // ✅ User role ID = 3
    PhoneNumber: "+1234567890"
);

// User entity has UserRoles navigation
var user = new User();
context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = 3 });

// DTO has Roles collection
result.Roles.Should().Contain(r => r.Name == "User");  // ✅ Check by name
```

---

## 🔢 System Role IDs Reference

```csharp
1 = SuperAdmin (IsSystemRole = true)
2 = Admin (IsSystemRole = true)
3 = User (IsSystemRole = true)
4+ = Custom roles created by users
```

---

## 🛠️ Migration Steps

### Step 1: Update IntegrationTestBase.cs

#### Before

```csharp
protected async Task<User> CreateTestUserAsync(
    string? email = null,
    string password = "Test123!",
    string firstName = "Test",
    string lastName = "User",
    UserRole role = UserRole.User)  // ❌ Enum parameter
{
    return await ExecuteDbContextAsync(async context =>
    {
        var user = new User
        {
            Email = email ?? GenerateUniqueEmail("testuser"),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            Role = role,  // ❌ Direct assignment
            Created = DateTime.UtcNow,
            IsArchived = false,
            Status = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    });
}
```

#### After

```csharp
protected async Task<User> CreateTestUserAsync(
    string? email = null,
    string password = "Test123!",
    string firstName = "Test",
    string lastName = "User",
    List<int>? roleIds = null)  // ✅ List of role IDs
{
    return await ExecuteDbContextAsync(async context =>
    {
        var user = new User
        {
            Email = email ?? GenerateUniqueEmail("testuser"),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            Created = DateTime.UtcNow,
            IsArchived = false,
            Status = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // ✅ Assign roles via junction table
        if (roleIds != null && roleIds.Any())
        {
            foreach (var roleId in roleIds)
            {
                context.UserRoles.Add(new Identity.Domain.Entities.UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId
                });
            }
            await context.SaveChangesAsync();
        }

        return user;
    });
}
```

### Step 2: Update CreateUserCommand Calls

#### Before

```csharp
var createCommand = new CreateUserCommand(
    Email: "newadmin@example.com",
    Password: "NewAdmin123!",
    FirstName: "New",
    LastName: "Admin",
    Role: UserRole.User,  // ❌ Enum parameter
    PhoneNumber: "+1234567890"
);
```

#### After

```csharp
var createCommand = new CreateUserCommand(
    Email: "newadmin@example.com",
    Password: "NewAdmin123!",
    FirstName: "New",
    LastName: "Admin",
    RoleIds: new List<int> { 3 },  // ✅ User role ID = 3
    PhoneNumber: "+1234567890"
);
```

### Step 3: Update UpdateUserCommand Calls

#### Before

```csharp
var updateCommand = new UpdateUserCommand(
    Id: userId,
    Email: "updated@example.com",
    FirstName: "Updated",
    LastName: "User",
    Role: UserRole.Admin,  // ❌ Enum parameter
    PhoneNumber: "+9876543210"
);
```

#### After

```csharp
var updateCommand = new UpdateUserCommand(
    Id: userId,
    Email: "updated@example.com",
    FirstName: "Updated",
    LastName: "User",
    RoleIds: new List<int> { 2 },  // ✅ Admin role ID = 2
    PhoneNumber: "+9876543210"
);
```

### Step 4: Update Assertions

#### Before

```csharp
// ❌ Direct Role property access
result.Role.Should().Be(UserRole.User);
```

#### After

```csharp
// ✅ Check Roles collection
result.Roles.Should().NotBeNull();
result.Roles.Should().Contain(r => r.Name == "User");

// Or check by ID
result.Roles.Should().Contain(r => r.Id == 3);
```

### Step 5: Update Direct Database User Creation

#### Before

```csharp
await ExecuteDbContextAsync(async context =>
{
    var user = new User
    {
        Email = "test@example.com",
        Role = UserRole.Admin,  // ❌ Removed property
        // ...
    };
    context.Users.Add(user);
    await context.SaveChangesAsync();
});
```

#### After

```csharp
await ExecuteDbContextAsync(async context =>
{
    var user = new User
    {
        Email = "test@example.com",
        // ...
    };
    context.Users.Add(user);
    await context.SaveChangesAsync();

    // ✅ Add role via junction table
    context.UserRoles.Add(new Identity.Domain.Entities.UserRole
    {
        UserId = user.Id,
        RoleId = 2  // Admin
    });
    await context.SaveChangesAsync();
});
```

---

## 📄 Files Requiring Migration

### 1. AdminEndpointsTests.cs

**Lines to Update:** ~136, 148, 161, 196, 215, 234, 258, 293, 334, 357, 379

**Pattern:**

```csharp
// Find all occurrences of:
Role: UserRole.User

// Replace with:
RoleIds: new List<int> { 3 }

// Find all occurrences of:
result.Role.Should().Be(UserRole.User)

// Replace with:
result.Roles.Should().Contain(r => r.Name == "User")
```

### 2. OtpAuthEndpointsTests.cs

**Lines to Update:** ~805, 843

**Pattern:**

```csharp
// Find:
user.Role = UserRole.Admin;

// Replace with:
context.UserRoles.Add(new Identity.Domain.Entities.UserRole
{
    UserId = user.Id,
    RoleId = 2  // Admin
});
await context.SaveChangesAsync();
```

### 3. IntegrationTestBase.cs

**Lines to Update:** ~50, 60

**Status:** ✅ Already fixed in latest update

---

## 🔄 Batch Find & Replace Commands

### For Visual Studio Code

```json
// Find (Regex enabled):
Role:\s*UserRole\.(User|Admin|SuperAdmin)

// Replace:
RoleIds: new List<int> { $1_TO_ID }

// Manual mapping:
User → 3
Admin → 2
SuperAdmin → 1
```

### For PowerShell Script

```powershell
# Create migration script
$files = Get-ChildItem -Path "*.cs" -Recurse

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw

    # Replace Role parameter
    $content = $content -replace 'Role:\s*UserRole\.User', 'RoleIds: new List<int> { 3 }'
    $content = $content -replace 'Role:\s*UserRole\.Admin', 'RoleIds: new List<int> { 2 }'
    $content = $content -replace 'Role:\s*UserRole\.SuperAdmin', 'RoleIds: new List<int> { 1 }'

    # Replace assertions
    $content = $content -replace 'result\.Role\.Should\(\)\.Be\(UserRole\.User\)', 'result.Roles.Should().Contain(r => r.Name == "User")'
    $content = $content -replace 'result\.Role\.Should\(\)\.Be\(UserRole\.Admin\)', 'result.Roles.Should().Contain(r => r.Name == "Admin")'

    Set-Content $file.FullName -Value $content
}
```

---

## ✅ Verification Checklist

After migration, verify:

- [ ] No compilation errors
- [ ] No `CS0618` warnings (obsolete UserRole enum)
- [ ] No `CS0117` errors (User.Role property doesn't exist)
- [ ] All tests compile successfully
- [ ] Run tests: `dotnet test --filter "FullyQualifiedName~AdminEndpointsTests"`
- [ ] Run tests: `dotnet test --filter "FullyQualifiedName~OtpAuthEndpointsTests"`
- [ ] Check database has UserRoles entries with correct role IDs

---

## 📊 Migration Impact

**Total Tests to Update:** ~13 tests across 2 files  
**Estimated Time:** 30-45 minutes  
**Risk Level:** Low (isolated to test files)

**Breaking Changes:** None - only affects test code, not production code

---

## 🐛 Common Issues & Solutions

### Issue 1: Obsolete UserRole Enum Warning

```
CS0618: 'UserRole' is obsolete: 'Use database-driven roles instead.'
```

**Solution:** Replace `UserRole.User` with `new List<int> { 3 }`

### Issue 2: User.Role Property Not Found

```
CS0117: 'User' does not contain a definition for 'Role'
```

**Solution:** Use UserRoles navigation property instead

### Issue 3: UserDto.Role Property Not Found

```
CS1061: 'UserDto' does not contain a definition for 'Role'
```

**Solution:** Use `Roles` collection instead of `Role` property

### Issue 4: Ambiguous UserRole Reference

```
CS0104: 'UserRole' is an ambiguous reference between 'Identity.Domain.Entities.UserRole' and 'IhsanDev.Shared.Kernel.Enums.Identity.UserRole'
```

**Solution:** Use fully qualified name or alias:

```csharp
using UserRoleEnum = IhsanDev.Shared.Kernel.Enums.Identity.UserRole;
// OR
context.UserRoles.Add(new Identity.Domain.Entities.UserRole { ... });
```

---

## 📝 Example Migration (Complete Test)

### Before

```csharp
[Fact]
public async Task CreateUser_WithValidData_ShouldCreateUser()
{
    // Arrange
    var createCommand = new CreateUserCommand(
        Email: "newadmin@example.com",
        Password: "NewAdmin123!",
        FirstName: "New",
        LastName: "Admin",
        Role: UserRole.User,  // ❌
        PhoneNumber: "+1234567890"
    );

    // Act
    var result = await SendAsync(createCommand);

    // Assert
    result.Should().NotBeNull();
    result.Email.Should().Be("newadmin@example.com");
    result.FirstName.Should().Be("New");
    result.LastName.Should().Be("Admin");
    result.Role.Should().Be(UserRole.User);  // ❌
}
```

### After

```csharp
[Fact]
public async Task CreateUser_WithValidData_ShouldCreateUser()
{
    // Arrange
    var createCommand = new CreateUserCommand(
        Email: "newadmin@example.com",
        Password: "NewAdmin123!",
        FirstName: "New",
        LastName: "Admin",
        RoleIds: new List<int> { 3 },  // ✅ User role
        PhoneNumber: "+1234567890"
    );

    // Act
    var result = await SendAsync(createCommand);

    // Assert
    result.Should().NotBeNull();
    result.Email.Should().Be("newadmin@example.com");
    result.FirstName.Should().Be("New");
    result.LastName.Should().Be("Admin");
    result.Roles.Should().Contain(r => r.Name == "User");  // ✅
    result.Roles.Should().HaveCount(1);
}
```

---

## 🎯 Completed Fixes

### Fix 1: SuperAdmin Protection Tests ✅

**Files Updated:** `RoleClaimEndpointsTests.cs` (lines 209, 285, 67)

**Problem:** Tests expected `BadRequestException` but handlers were throwing `ForbiddenException` for system role protection.

**Solution:**

```csharp
// Line 209 - UpdateRole_SystemRole_ShouldThrowForbiddenException
await Assert.ThrowsAsync<ForbiddenException>(async () =>
    await _mediator.Send(new UpdateRoleCommand(superAdminRole.Id, "NewName", "New Desc"), ct));

// Line 285 - DeleteRole_SystemRole_ShouldThrowForbiddenException
await Assert.ThrowsAsync<ForbiddenException>(async () =>
    await _mediator.Send(new DeleteRoleCommand(superAdminRole.Id), ct));
```

**Impact:** Fixed 2 test failures

### Fix 2: Admin Handler Navigation Properties ✅

**Files Updated:** `CreateUserCommandHandler.cs`, `UpdateUserCommandHandler.cs`

**Problem:** Tests throwing `GeneralException` because `User.UserRoles` navigation property was null after role assignment.

**Solution:** Reload entity from database after role assignment.

```csharp
// CreateUserCommandHandler.cs (after line 73)
await _userRoleRepository.AssignRolesToUserAsync(user.Id, request.RoleIds, cancellationToken);

// Reload user with roles navigation property populated
var userWithRoles = await _userRepository.GetByIdAsync(user.Id, cancellationToken);
if (userWithRoles == null)
    throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

var userDto = UserDto.MapFrom(userWithRoles);
```

**Impact:** Fixed 5 test failures

### Fix 3: Test Results ✅

**Final Results:**

```
Total: 142 tests
Passed: 142 tests (100%)
Failed: 0 tests
Build: 18/18 projects succeeded
Time: 2.4 seconds
```

**All test categories passing:**

- ✅ Authentication tests (login, register, refresh, logout)
- ✅ Admin user management (CRUD)
- ✅ Role management (CRUD, system protection)
- ✅ Claim management (CRUD)
- ✅ Role-claim assignments
- ✅ Multi-tenancy scenarios
- ✅ JWT validation

---

## 🚀 Migration Completed

**Status:** ✅ All tests migrated and passing

**Next Steps:**

1. ✅ Run migration script - **DONE**
2. ✅ Fix compilation errors - **DONE**
3. ✅ Run tests to verify - **142/142 PASSING**
4. ✅ Update documentation - **DONE**
5. ✅ Mark task complete - **COMPLETE**

**For detailed changes, see:** [IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md](IDENTITY_SERVICE_IMPROVEMENTS_JANUARY_2026.md)

---

**Status:** ✅ Complete (Jan 12, 2026)  
**Test Coverage:** 142/142 passing (100%)  
**Priority:** Complete - No action required
