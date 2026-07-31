using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Application.Services;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDtoIncludesToken>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<LoginCommandHandler> _logger;
    private readonly int _maxFailedAttempts;
    private readonly int _lockoutMinutes;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IUserService userService,
        ProfilePictureHelper profilePictureHelper,
        ILocalizationService localizationService,
        IConfiguration configuration,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userService = userService;
        _profilePictureHelper = profilePictureHelper;
        _localizationService = localizationService;
        _logger = logger;
        _maxFailedAttempts = configuration.GetValue("LoginSecurity:MaxFailedAttempts", 5);
        _lockoutMinutes = configuration.GetValue("LoginSecurity:LockoutMinutes", 15);
    }

    public async Task<UserDtoIncludesToken> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (user == null)
                throw new UnauthorizedException(LocalizationKeys.Exceptions.InvalidCredentials);

            if (!user.Status)
                throw new ForbiddenException(LocalizationKeys.Exceptions.AccountDisabled);

            // Checked before verifying the password so a locked-out account never gets a fresh
            // attempt counted, and a locked-out account can't be brute-forced during the lockout window.
            if (user.LoginLockoutUntil.HasValue && user.LoginLockoutUntil.Value > DateTime.UtcNow)
            {
                var remainingMinutes = (int)(user.LoginLockoutUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
                throw new ForbiddenException(LocalizationKeys.Otp.AccountLockedWithMinutes, _localizationService, remainingMinutes);
            }

            if (string.IsNullOrEmpty(user.PasswordHash) || !_userService.VerifyPassword(request.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts += 1;

                if (user.FailedLoginAttempts >= _maxFailedAttempts)
                {
                    user.LoginLockoutUntil = DateTime.UtcNow.AddMinutes(_lockoutMinutes);
                    await _userRepository.UpdateAsync(user, cancellationToken);
                    throw new ForbiddenException(LocalizationKeys.Otp.AccountLockedWithMinutes, _localizationService, _lockoutMinutes);
                }

                user.LastModified = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user, cancellationToken);
                throw new UnauthorizedException(LocalizationKeys.Exceptions.InvalidCredentials);
            }

            user.FailedLoginAttempts = 0;
            user.LoginLockoutUntil = null;
            user.LastLogin = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);

            var authResult = await _userService.GenerateTokensAsync(user);

            await _profilePictureHelper.EnrichWithProfilePictureAsync(
                authResult,
                user.ProfilePictureId,
                user.Id,
                cancellationToken);

            return authResult;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
