using System.Reflection;

namespace Tenant.Application.Common.Mappings;

/// <summary>
/// AutoMapper profile for Tenant.Application assembly
/// Scans the assembly for types implementing IMapFrom<T> and registers their mappings
/// </summary>
public class MappingProfile : IhsanDev.Shared.Application.Common.Mappings.MappingProfile
{
    public MappingProfile() : base(Assembly.GetExecutingAssembly())
    {
        // This will scan the Tenant.Application assembly for all DTOs implementing IMapFrom<T>
        // and automatically register their mappings
    }
}
