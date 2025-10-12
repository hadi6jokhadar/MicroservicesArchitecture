using System.Security.Claims;
using Identity.Domain.Entities;

namespace Identity.Application.Services;

public interface IJwtTokenGenerator
{
    (string AccessToken, string RefreshToken, DateTime ExpiresAt) GenerateTokens(User user);
    ClaimsPrincipal? ValidateToken(string token);
}