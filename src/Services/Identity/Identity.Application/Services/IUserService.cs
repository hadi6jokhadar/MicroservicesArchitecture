using Identity.Application.DTOs;
using Identity.Domain.Entities;

namespace Identity.Application.Services;

public interface IUserService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    Task<UserDtoIncludesToken> GenerateTokensAsync(User user);
    Task<UserDtoIncludesToken?> RefreshTokenAsync(string refreshToken);
    Task<bool> RevokeTokenAsync(int userId);
    string GeneratePasswordResetToken();
    bool IsValidEmail(string email);
}