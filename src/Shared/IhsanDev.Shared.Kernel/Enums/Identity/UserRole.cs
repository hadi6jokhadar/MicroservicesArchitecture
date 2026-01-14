namespace IhsanDev.Shared.Kernel.Enums.Identity;

/// <summary>
/// DEPRECATED: This enum is no longer used in Identity Service.
/// Roles are now managed in the database with the Role entity.
/// This enum is kept only for backward compatibility with other services.
/// </summary>
[Obsolete("Use database-driven roles instead. See Identity.Domain.Entities.Role entity.")]
public enum UserRole
{
    User = 1,
    Admin = 2,
    SuperAdmin = 3
}