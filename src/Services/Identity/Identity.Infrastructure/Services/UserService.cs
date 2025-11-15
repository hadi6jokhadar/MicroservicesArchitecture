using Identity.Application.DTOs;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace Identity.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IConfiguration configuration, 
        IUserRepository userRepository, 
        IPasswordHasher passwordHasher,
        ITenantContext tenantContext,
        ILogger<UserService> logger)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return _passwordHasher.VerifyPassword(password, hash);
    }

    public async Task<UserDtoIncludesToken> GenerateTokensAsync(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        
        // Determine JWT settings based on MultiTenancy mode
        var multiTenancyEnabled = _configuration.GetValue<bool>("MultiTenancy:Enabled", false);
        string jwtSecret;
        string jwtIssuer;
        string jwtAudience;
        int expiryMinutes;
        int refreshTokenExpiryDays;
        
        if (multiTenancyEnabled)
        {
            // When multi-tenancy is enabled, ONLY use tenant-specific JWT settings
            if (!_tenantContext.HasTenant || 
                _tenantContext.CurrentTenant?.Configuration?.Jwt == null ||
                string.IsNullOrWhiteSpace(_tenantContext.CurrentTenant.Configuration.Jwt.Secret))
            {
                throw new InvalidOperationException(
                    "Multi-tenancy is enabled but tenant JWT configuration is not available. " +
                    "Ensure x-tenant-id header is provided and tenant exists with valid JWT settings.");
            }

            // Use tenant-specific JWT settings
            var tenantJwt = _tenantContext.CurrentTenant.Configuration.Jwt;
            jwtSecret = tenantJwt.Secret!;
            jwtIssuer = tenantJwt.Issuer ?? "IdentityService";
            jwtAudience = tenantJwt.Audience ?? "MicroservicesApp";
            expiryMinutes = tenantJwt.AccessTokenExpirationMinutes > 0 
                ? tenantJwt.AccessTokenExpirationMinutes 
                : 60;
            refreshTokenExpiryDays = tenantJwt.RefreshTokenExpirationDays > 0 
                ? tenantJwt.RefreshTokenExpirationDays 
                : 7;
        }
        else
        {
            // When multi-tenancy is disabled, use appsettings.json
            jwtSecret = _configuration["Jwt:Secret"] 
                ?? throw new InvalidOperationException("JWT Secret is not configured in appsettings.json");
            jwtIssuer = _configuration["Jwt:Issuer"] ?? "IdentityService";
            jwtAudience = _configuration["Jwt:Audience"] ?? "MicroservicesApp";
            expiryMinutes = _configuration.GetValue<int>("Jwt:AccessTokenExpirationMinutes", 60);
            refreshTokenExpiryDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpirationDays", 7);
        }
        
        var key = Encoding.UTF8.GetBytes(jwtSecret);
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        
        // Add tenant ID claim if available
        if (_tenantContext.HasTenant && _tenantContext.CurrentTenant != null)
        {
            claims.Add(new Claim("tenant_id", _tenantContext.CurrentTenant.TenantId));
            _logger.LogDebug("Added tenant_id claim: {TenantId}", _tenantContext.CurrentTenant.TenantId);
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = jwtIssuer,
            Audience = jwtAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);
        
        var refreshToken = GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);
        
        await _userRepository.UpdateRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiry);

        // Manual mapping
        var authenticationResult = UserDtoIncludesToken.MapFrom(user);
    
        authenticationResult.AccessToken = accessToken;
        authenticationResult.RefreshToken = refreshToken;
        authenticationResult.RefreshTokenExpiryTime = tokenDescriptor.Expires.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        
        return authenticationResult;
    }

    public async Task<UserDtoIncludesToken?> RefreshTokenAsync(string refreshToken)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(refreshToken);
        
        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return null;
        }

        return await GenerateTokensAsync(user);
    }

    public async Task<bool> RevokeTokenAsync(int userId)
    {
        return await _userRepository.RevokeRefreshTokenAsync(userId);
    }

    public string GeneratePasswordResetToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private string GenerateRefreshToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[64];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}