namespace IhsanDev.Shared.Application.Localization;

/// <summary>
/// Static class containing all localization keys
/// Usage: _localizationService.GetString(LocalizationKeys.Exceptions.BadRequest)
/// </summary>
public static class LocalizationKeys
{
    /// <summary>
    /// Exception message keys
    /// </summary>
    public static class Exceptions
    {
        public const string BadRequest = "exception_bad_request";
        public const string Unauthorized = "exception_unauthorized";
        public const string Forbidden = "exception_forbidden";
        public const string NotFound = "exception_not_found";
        public const string Conflict = "exception_conflict";
        public const string InternalServerError = "exception_internal_server_error";
        
        // Specific exceptions
        public const string UserNotFound = "exception_user_not_found";
        public const string InvalidCredentials = "exception_invalid_credentials";
        public const string EmailAlreadyExists = "exception_email_already_exists";
        public const string PhoneAlreadyRegistered = "exception_phone_already_registered";
        public const string InvalidToken = "exception_invalid_token";
        public const string ExpiredToken = "exception_expired_token";
        public const string TenantNotFound = "exception_tenant_not_found";
        public const string TenantNotFoundForUser = "exception_tenant_not_found_for_user";
        public const string FileNotFound = "exception_file_not_found";
        public const string FileNotFoundOnDisk = "exception_file_not_found_on_disk";
        public const string InvalidFileType = "exception_invalid_file_type";
        public const string FileSizeExceeded = "exception_file_size_exceeded";
        public const string AccountDisabled = "exception_account_disabled";
        public const string QueueItemNotFound = "exception_queue_item_not_found";
        public const string NotificationNotFound = "exception_notification_not_found";
        public const string TenantIdMismatch = "exception_tenant_id_mismatch";
        public const string FileEmpty = "exception_file_empty";
        public const string InvalidUserId = "exception_invalid_user_id";
        public const string TenantContextRequired = "exception_tenant_context_required";
        public const string RoleNotFound = "exception_role_not_found";
        public const string RoleAlreadyExists = "exception_role_already_exists";
        public const string ClaimNotFound = "exception_claim_not_found";
        public const string ClaimAlreadyExists = "exception_claim_already_exists";
        public const string SystemRoleCannotBeRenamed = "exception_system_role_cannot_be_renamed";
        public const string SystemRoleCannotBeDeleted = "exception_system_role_cannot_be_deleted";
        public const string SuperAdminRoleProtected = "exception_superadmin_role_protected";
        public const string SuperAdminClaimProtected = "exception_superadmin_claim_protected";
    }

    /// <summary>
    /// Validation message keys
    /// </summary>
    public static class Validation
    {
        public const string Required = "validation_required";
        public const string EmailInvalid = "validation_email_invalid";
        public const string PasswordTooShort = "validation_password_too_short";
        public const string PasswordRequiresDigit = "validation_password_requires_digit";
        public const string PasswordRequiresUppercase = "validation_password_requires_uppercase";
        public const string PasswordRequiresLowercase = "validation_password_requires_lowercase";
        public const string PasswordRequiresNonAlphanumeric = "validation_password_requires_non_alphanumeric";
        public const string MaxLength = "validation_max_length";
        public const string MinLength = "validation_min_length";
        public const string MustBeGreaterThan = "validation_must_be_greater_than";
        public const string MustBeGreaterThanOrEqual = "validation_must_be_greater_than_or_equal";
        public const string MustBeLessThan = "validation_must_be_less_than";
        public const string MustBeLessThanOrEqual = "validation_must_be_less_than_or_equal";
        public const string InvalidFormat = "validation_invalid_format";
        public const string PhoneNumberInvalid = "validation_phone_number_invalid";
        public const string InvalidRole = "validation_invalid_role";
        public const string InvalidDeliveryType = "validation_invalid_delivery_type";
        public const string InvalidPriority = "validation_invalid_priority";
        public const string InvalidPlatform = "validation_invalid_platform";
        public const string DateRangeInvalid = "validation_date_range_invalid";
        public const string PageSizeExceeded = "validation_page_size_exceeded";
        public const string TenantIdFormat = "validation_tenant_id_format";
        public const string MustBeAfter = "validation_must_be_after";
    }

    /// <summary>
    /// Success message keys
    /// </summary>
    public static class Success
    {
        public const string RegistrationSuccessful = "success_registration_successful";
        public const string LoginSuccessful = "success_login_successful";
        public const string LogoutSuccessful = "success_logout_successful";
        public const string ProfileUpdated = "success_profile_updated";
        public const string PasswordChanged = "success_password_changed";
        public const string EmailSent = "success_email_sent";
        public const string PasswordResetEmailSent = "success_password_reset_email_sent";
        public const string FileUploaded = "success_file_uploaded";
        public const string FileDeleted = "success_file_deleted";
        public const string NotificationSent = "success_notification_sent";
        public const string VerificationCodeSentPhone = "success_verification_code_sent_phone";
        public const string VerificationCodeSentEmail = "success_verification_code_sent_email";
        public const string RegistrationSuccessfulLoginPhone = "success_registration_successful_login_phone";
        public const string RegistrationSuccessfulLoginEmail = "success_registration_successful_login_email";
        public const string TenantDeleted = "success_tenant_deleted";
        public const string NotificationMarkedAsRead = "success_notification_marked_as_read";
    }

    /// <summary>
    /// Common UI message keys
    /// </summary>
    public static class Common
    {
        public const string Save = "common_save";
        public const string Cancel = "common_cancel";
        public const string Delete = "common_delete";
        public const string Edit = "common_edit";
        public const string Create = "common_create";
        public const string Update = "common_update";
        public const string Search = "common_search";
        public const string Filter = "common_filter";
        public const string Export = "common_export";
        public const string Import = "common_import";
        public const string Loading = "common_loading";
        public const string NoData = "common_no_data";
        public const string Confirm = "common_confirm";
        public const string Yes = "common_yes";
        public const string No = "common_no";
    }

    /// <summary>
    /// Notification message keys
    /// </summary>
    public static class Notifications
    {
        public const string WelcomeTitle = "notification_welcome_title";
        public const string WelcomeMessage = "notification_welcome_message";
        public const string NewLoginTitle = "notification_new_login_title";
        public const string NewLoginMessage = "notification_new_login_message";
        public const string PasswordChangedTitle = "notification_password_changed_title";
        public const string PasswordChangedMessage = "notification_password_changed_message";
    }

    /// <summary>
    /// OTP/Phone verification message keys
    /// </summary>
    public static class Otp
    {
        public const string CodeSent = "otp_code_sent";
        public const string CodeExpired = "otp_code_expired";
        public const string CodeInvalid = "otp_code_invalid";
        public const string MaxAttemptsReached = "otp_max_attempts_reached";
        public const string AccountLocked = "otp_account_locked";
        public const string AccountLockedWithMinutes = "otp_account_locked_with_minutes";
        public const string RemainingAttempts = "otp_remaining_attempts";
        public const string ResendCooldown = "otp_resend_cooldown";
    }

    /// <summary>
    /// System error message keys
    /// </summary>
    public static class Error
    {
        public const string RateLimitExceeded = "error_rate_limit_exceeded";
        public const string TenantContextRequired = "error_tenant_context_required";
    }

    /// <summary>
    /// Tenant-related message keys
    /// </summary>
    public static class Tenant
    {
        public const string MissingHeader = "tenant_missing_header";
        public const string MissingHeaderMessage = "tenant_missing_header_message";
        public const string MissingHeaderDetails = "tenant_missing_header_details";
        public const string NotFoundOrInactive = "tenant_not_found_or_inactive";
        public const string NotActive = "tenant_not_active";
        public const string ConfigurationError = "tenant_configuration_error";
    }

    /// <summary>
    /// CORS-related message keys
    /// </summary>
    public static class Cors
    {
        public const string OriginNotAllowed = "cors_origin_not_allowed";
    }
}
