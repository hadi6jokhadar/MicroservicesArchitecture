using AutoMapper;
using Identity.Application.DTOs;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    
    private readonly IMapper _mapper;

    public UserService(IConfiguration configuration, IUserRepository userRepository, IPasswordHasher passwordHasher, IMapper mapper)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
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
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "your-secret-key-here");
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpiryInMinutes"] ?? "60")),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);
        
        var refreshToken = GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpiryInDays"] ?? "7"));
        
        await _userRepository.UpdateRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiry);

        var authenticationResult = _mapper.Map<UserDtoIncludesToken>(user);
    
        authenticationResult.AccessToken = accessToken;
        authenticationResult.RefreshToken = refreshToken;
        authenticationResult.RefreshTokenExpiryTime = tokenDescriptor.Expires.Value;
        
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