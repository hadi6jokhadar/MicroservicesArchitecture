using IhsanDev.Shared.Application.Common.Mappings;

namespace Identity.Application.Common.Mappings;

public class IdentityMappingProfile : MappingProfile
{
    public IdentityMappingProfile() : base(typeof(IdentityMappingProfile).Assembly)
    {
    }
}