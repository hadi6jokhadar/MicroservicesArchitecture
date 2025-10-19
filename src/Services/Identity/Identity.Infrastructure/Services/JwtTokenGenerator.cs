using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Infrastructure.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace Identity.Infrastructure.Services;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;

    public JwtTokenGenerator(IConfiguration configuration, ITenantContext tenantContext)
    {
        _configuration = configuration;
        _tenantContext = tenantContext;
    }

    public (string AccessToken, string RefreshToken, DateTime ExpiresAt) GenerateTokens(User user)
    {
        // ✨ Single line to get JWT settings - handles tenant/default automatically!
        var jwtSettings = ConfigurationHelper.GetJwtSettings(_configuration, _tenantContext);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add tenant ID to claims if in multi-tenant mode
        if (_tenantContext.HasTenant && _tenantContext.TenantId != null)
        {
            claims = claims.Append(new Claim("tenant_id", _tenantContext.TenantId)).ToArray();
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();

        return (accessToken, refreshToken, expiresAt);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        // ✨ Single line to get JWT settings - handles tenant/default automatically!
        var jwtSettings = ConfigurationHelper.GetJwtSettings(_configuration, _tenantContext);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}