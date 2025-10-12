using Identity.Application.Services;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Domain.Entities;
using IhsanDev.Shared.Kernel.Enums.Identity;
using IhsanDev.Shared.Application.Exceptions;

namespace Identity.Application.Handlers;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthenticationResult>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<AuthenticationResult>> Handle(
        RegisterCommand request, 
        CancellationToken cancellationToken)
    {
        // Check if email exists
        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new ConflictException("Email is already registered");
        }

        // Create user
        var user = new User
        {
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Role = UserRole.User
        };

        await _userRepository.AddAsync(user, cancellationToken);

        // Generate tokens
        var (accessToken, refreshToken, expiresAt) = _jwtTokenGenerator.GenerateTokens(user);

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = expiresAt.AddDays(7);
        await _userRepository.UpdateAsync(user, cancellationToken);

        var result = new AuthenticationResult(
            accessToken,
            refreshToken,
            expiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString())
        );

        return Result<AuthenticationResult>.Success(result);
    }
}