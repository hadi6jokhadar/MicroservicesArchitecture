using IhsanDev.Shared.Infrastructure.Extensions;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Http;

namespace IhsanDev.Shared.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.GetUserId();

    public string? Email => _httpContextAccessor.HttpContext?.User?.GetEmail();

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated 
        ?? false;

    public bool IsSuperAdmin => _httpContextAccessor.HttpContext?.User?.IsSuperAdmin() ?? false;

    public IEnumerable<string> Roles => _httpContextAccessor.HttpContext?.User?.GetRoles() 
        ?? Enumerable.Empty<string>();

    public bool HasRole(string role) => _httpContextAccessor.HttpContext?.User?.HasRole(role) ?? false;
}