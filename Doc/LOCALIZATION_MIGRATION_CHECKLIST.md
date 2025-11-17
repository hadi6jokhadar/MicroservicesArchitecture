# 🔄 Localization Migration Checklist

**Auto-generated migration plan for updating all services to use localization**

**Date:** November 17, 2025

---

## 📊 Files Requiring Updates

### **Identity Service**

#### Validators (11 files)

- ✅ `Commands/Admin/DeleteUserCommandValidator.cs` - UPDATED
- ⏳ `Commands/Admin/CreateUserCommand.cs` - IN PROGRESS
- ⏳ All other validators need update

#### Handlers - Exception Messages (20+ files)

**Pattern to Replace:**

```csharp
// OLD
throw new ConflictException("User with this email already exists");
throw new NotFoundException("User not found");
throw new UnauthorizedException("Invalid email or password");
throw new ForbiddenException("Account is disabled");
throw new GeneralException("Failed to create user: " + ex.Message);

// NEW
throw new ConflictException(LocalizationKeys.Exceptions.EmailAlreadyExists);
throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);
throw new UnauthorizedException(LocalizationKeys.Exceptions.InvalidCredentials);
throw new ForbiddenException(LocalizationKeys.Exceptions.Forbidden);
throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
```

**Files:**

1. `Handlers/Admin/CreateUserCommandHandler.cs`
2. `Handlers/Admin/DeleteUserCommandHandler.cs`
3. `Handlers/Admin/GetUserByIdCommandHandler.cs`
4. `Handlers/Admin/UpdateUserCommandHandler.cs`
5. `Handlers/Admin/ToggleUserStatusCommandHandler.cs`
6. `Handlers/Admin/GetUsersCommandHandler.cs`
7. `Handlers/Auth/LoginCommandHandler.cs`
8. `Handlers/Auth/RegisterCommandHandler.cs`
9. `Handlers/Auth/LoginWithCodeByPhoneCommandHandler.cs`
10. `Handlers/Auth/LoginWithCodeByEmailCommandHandler.cs`
11. `Handlers/Auth/RegisterWithCodeByPhoneCommandHandler.cs`
12. `Handlers/Auth/RegisterWithCodeByEmailCommandHandler.cs`
13. `Handlers/Auth/GetVerificationCodeByPhoneCommandHandler.cs`
14. `Handlers/Auth/GetVerificationCodeByEmailCommandHandler.cs`
15. `Handlers/Auth/RefreshTokenCommandHandler.cs`
16. `Handlers/Auth/ForgetPasswordCommandHandler.cs`
17. `Handlers/User/GetUserProfileCommandHandler.cs`
18. `Handlers/User/UpdateProfileCommandHandler.cs`

#### API Handlers - Success Messages (5 files)

**Pattern to Replace:**

```csharp
// OLD
return Results.Ok(new { success = result, message = "Verification code sent successfully to your phone" });
return Results.Ok(new { message = "Logged out successfully" });

// NEW (inject ILocalizationService)
return Results.Ok(new { success = result, message = _localization.GetString(LocalizationKeys.Otp.CodeSent, "phone") });
return Results.Ok(new { message = _localization.GetString(LocalizationKeys.Success.LogoutSuccessful) });
```

**Files:**

1. `API/Handlers/AuthApiHandlers.cs`
2. `API/Handlers/UserApiHandlers.cs`
3. `API/Handlers/AdminApiHandlers.cs`

---

### **Notification Service**

#### Validators (6 files)

1. `Commands/SendNotificationCommand.cs`
2. `Commands/GetUserNotificationsCommand.cs`
3. `Commands/MarkNotificationAsReadCommand.cs`
4. `Commands/GetQueueItemsCommand.cs`
5. `Commands/GetQueueItemStatusCommand.cs`
6. `Commands/AcknowledgeNotificationCommand.cs`

#### API Handlers - Messages (3 files)

1. `API/Handlers/NotificationApiHandlers.cs`
2. `API/Program.cs` (rate limit message)

---

### **Tenant Service**

#### Validators (1 file)

1. `Commands/Tenant/UpdateTenantCommand.cs`

#### Handlers - Exception Messages (6 files)

1. `Handlers/Tenant/CreateTenantCommandHandler.cs`
2. `Handlers/Tenant/UpdateTenantCommandHandler.cs`
3. `Handlers/Tenant/DeleteTenantCommandHandler.cs`
4. `Handlers/Tenant/TenantQueryHandlers.cs` (5 methods)

#### API Handlers - Messages (1 file)

1. `API/Handlers/TenantApiHandlers.cs`

---

### **FileManager Service**

#### Validators (4 files)

1. `Handlers/SaveFile/SaveFileCommandValidator.cs`
2. `Handlers/GetFiles/GetFilesQueryValidator.cs`
3. `Handlers/UpdateFile/UpdateFileCommandValidator.cs`
4. `Handlers/DeleteTempFiles/DeleteOldTempFilesCommandValidator.cs`

#### Exception Messages

1. `Infrastructure/Services/FileManagerService.cs` - "File is empty or null."

---

## 🎯 Migration Strategy

### Phase 1: Update All Validators (Priority: HIGH)

- Convert from `AbstractValidator<T>` to `LocalizedValidator<T>`
- Inject `ILocalizationService` in constructor
- Replace all hardcoded `WithMessage("...")` with `WithMessage(L(LocalizationKeys...))`

### Phase 2: Update All Exception Throws (Priority: HIGH)

- Replace hardcoded exception messages with `LocalizationKeys`
- Use pattern: `throw new XException(LocalizationKeys.Exceptions.Y);`
- Remove concatenated error messages like `"Failed to X: " + ex.Message"`

### Phase 3: Update API Response Messages (Priority: MEDIUM)

- Inject `ILocalizationService` into API handlers
- Replace hardcoded success messages with localized keys
- Update all `return Results.Ok(new { message = "..." })`

### Phase 4: Add Missing Localization Keys (Priority: MEDIUM)

- Review all unique messages
- Add missing keys to `LocalizationKeys.cs`
- Add translations to `en.json` and `ar.json`

---

## 📝 Additional Localization Keys Needed

Based on the codebase scan, these additional keys are needed:

### Exceptions

```csharp
public const string PhoneAlreadyRegistered = "exception_phone_already_registered";
public const string EmailOrPasswordIncorrect = "exception_email_or_password_incorrect";
public const string PhoneOrCodeIncorrect = "exception_phone_or_code_incorrect";
public const string EmailOrCodeIncorrect = "exception_email_or_code_incorrect";
public const string AccountDisabled = "exception_account_disabled";
public const string QueueItemNotFound = "exception_queue_item_not_found";
public const string NotificationNotFound = "exception_notification_not_found";
public const string TenantIdMismatch = "exception_tenant_id_mismatch";
public const string FileEmpty = "exception_file_empty";
public const string InvalidUserId = "exception_invalid_user_id";
```

### Success Messages

```csharp
public const string VerificationCodeSentPhone = "success_verification_code_sent_phone";
public const string VerificationCodeSentEmail = "success_verification_code_sent_email";
public const string RegistrationSuccessfulLoginPhone = "success_registration_successful_login_phone";
public const string RegistrationSuccessfulLoginEmail = "success_registration_successful_login_email";
public const string TenantDeleted = "success_tenant_deleted";
public const string NotificationMarkedAsRead = "success_notification_marked_as_read";
```

### Validation

```csharp
public const string InvalidRole = "validation_invalid_role";
public const string InvalidDeliveryType = "validation_invalid_delivery_type";
public const string InvalidPriority = "validation_invalid_priority";
public const string DateRangeInvalid = "validation_date_range_invalid";
public const string PageSizeExceeded = "validation_page_size_exceeded";
```

### Other

```csharp
public const string RateLimitExceeded = "error_rate_limit_exceeded";
public const string TenantContextRequired = "error_tenant_context_required";
```

---

## ✅ Completed Tasks

- ✅ Created localization infrastructure
- ✅ Created LocalizationService
- ✅ Created LocalizationKeys class
- ✅ Created en.json and ar.json files
- ✅ Created LocalizationMiddleware
- ✅ Created GlobalExceptionHandlingMiddleware
- ✅ Updated AppException classes
- ✅ Created LocalizedValidator base class
- ✅ Updated DeleteUserCommandValidator (example)

---

## 🔄 Next Steps

1. **Extend LocalizationKeys** - Add missing keys listed above
2. **Update en.json and ar.json** - Add translations for new keys
3. **Batch Update Validators** - Convert all validators to LocalizedValidator
4. **Batch Update Handlers** - Replace all exception throws
5. **Update API Handlers** - Inject localization and update messages
6. **Test Each Service** - Verify all messages are localized
7. **Update Integration Tests** - Adjust tests for new response format

---

## 📊 Progress Tracking

| Service      | Validators | Handlers | API Handlers | Status         |
| ------------ | ---------- | -------- | ------------ | -------------- |
| Identity     | 10%        | 0%       | 0%           | 🟡 In Progress |
| Notification | 0%         | 0%       | 0%           | ⚪ Not Started |
| Tenant       | 0%         | 0%       | 0%           | ⚪ Not Started |
| FileManager  | 0%         | 0%       | 0%           | ⚪ Not Started |

**Total Progress:** 2% Complete

---

## 🎯 Estimated Effort

- **Validators**: ~30 files × 5 min = 2.5 hours
- **Handlers**: ~40 files × 10 min = 6.5 hours
- **API Handlers**: ~10 files × 15 min = 2.5 hours
- **Testing**: 2 hours
- **Total**: ~13.5 hours

---

**Status:** ⚡ Ready to proceed with systematic migration
