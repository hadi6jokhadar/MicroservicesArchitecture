using System.Globalization;

namespace Identity.Application.DTOs;

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool Status { get; set; }
    public List<ClaimDto>? Claims { get; set; }

    public static RoleDto MapFrom(Domain.Entities.Role role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            Status = role.Status,
            Claims = role.RoleClaims?.Select(rc => new ClaimDto
            {
                Id = rc.Claim.Id,
                Name = rc.Claim.Name,
                Description = rc.Claim.Description,
                ClaimType = rc.Claim.ClaimType,
                ClaimValue = rc.Claim.ClaimValue,
                IsSuperAdminOnly = rc.Claim.IsSuperAdminOnly,
                Status = rc.Claim.Status
            }).ToList()
        };
    }
}

public class ClaimDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ClaimType { get; set; } = string.Empty;
    public string ClaimValue { get; set; } = string.Empty;
    public bool IsSuperAdminOnly { get; set; }
    public bool Status { get; set; }

    public static ClaimDto MapFrom(Domain.Entities.Claim claim)
    {
        return new ClaimDto
        {
            Id = claim.Id,
            Name = claim.Name,
            Description = claim.Description,
            ClaimType = claim.ClaimType,
            ClaimValue = claim.ClaimValue,
            IsSuperAdminOnly = claim.IsSuperAdminOnly,
            Status = claim.Status
        };
    }
}
