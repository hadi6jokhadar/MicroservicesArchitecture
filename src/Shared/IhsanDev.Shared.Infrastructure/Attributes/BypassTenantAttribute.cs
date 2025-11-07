namespace IhsanDev.Shared.Infrastructure.Attributes;

/// <summary>
/// Attribute to mark endpoints that should bypass tenant resolution.
/// Use this on endpoints that operate across all tenants (e.g., SuperAdmin endpoints).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class BypassTenantAttribute : Attribute
{
}
