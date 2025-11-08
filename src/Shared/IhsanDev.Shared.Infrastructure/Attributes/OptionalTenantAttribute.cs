namespace IhsanDev.Shared.Infrastructure.Attributes;

/// <summary>
/// Marks an endpoint where tenant context is optional
/// The tenant middleware will run and set tenant context if x-tenant-id is provided,
/// but will NOT fail if the header is missing
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class OptionalTenantAttribute : Attribute
{
}
