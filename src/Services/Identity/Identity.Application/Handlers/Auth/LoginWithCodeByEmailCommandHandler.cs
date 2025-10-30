using Identity.Application.Commands.Auth;
using Identity.Application.DTOs;
using Identity.Application.Services;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using MediatR;

namespace Identity.Application.Handlers.Auth;

public class LoginWithCodeByEmailCommandHandler : IRequestHandler<LoginWithCodeByEmailCommand, UserDtoIncludesToken>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;

    public LoginWithCodeByEmailCommandHandler(
        IUserRepository userRepository,
        IUserService userService)
    {
        _userRepository = userRepository;
        _userService = userService;
    }

    public async Task<UserDtoIncludesToken> Handle(LoginWithCodeByEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Find user by email
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (user == null)
            {
                throw new UnauthorizedException("Email or verification code is incorrect");
            }

            if (!user.Status)
            {
                throw new ForbiddenException("Account is disabled");
            }

            // Verify code matches database
            if (string.IsNullOrEmpty(user.VerificationCode) || 
                user.VerificationCode != request.VerificationCode)
            {
                throw new UnauthorizedException("Email or verification code is incorrect");
            }

            // Clear verification code after successful login
            user.VerificationCode = null;
            user.LastLogin = DateTime.UtcNow;
            user.LastModified = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);

            // Generate and return tokens
            var authResult = await _userService.GenerateTokensAsync(user);
            return authResult;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Login with email code failed: " + ex.Message);
        }
    }
}
