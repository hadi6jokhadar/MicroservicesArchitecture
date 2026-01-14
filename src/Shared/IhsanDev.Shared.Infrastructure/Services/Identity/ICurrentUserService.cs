namespace IhsanDev.Shared.Infrastructure.Services.Identity;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }
    IEnumerable<string> Roles { get; }
    bool HasRole(string role);
}