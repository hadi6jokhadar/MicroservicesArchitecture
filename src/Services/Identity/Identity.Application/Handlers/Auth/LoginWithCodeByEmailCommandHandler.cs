using Identity.Application.Commands.Auth;
using Identity.Application.DTOs;
using Identity.Application.Services;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Identity.Application.Handlers.Auth;

public class LoginWithCodeByEmailCommandHandler : IRequestHandler<LoginWithCodeByEmailCommand, UserDtoIncludesToken>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;

    public LoginWithCodeByEmailCommandHandler(
        IUserRepository userRepository,
        IUserService userService,
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _userService = userService;
        _configuration = configuration;
        _tenantContext = tenantContext;
    }

    public async Task<UserDtoIncludesToken> Handle(LoginWithCodeByEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get OTP settings from tenant or appsettings
            var otpSettings = GetOtpSettings();

            // Find user by email
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (user == null)
            {
                throw new UnauthorizedException(LocalizationKeys.Exceptions.InvalidCredentials);
            }

            if (!user.Status)
            {
                throw new ForbiddenException(LocalizationKeys.Exceptions.AccountDisabled);
            }

            // Check if user is locked out
            if (user.CodeLockoutUntil.HasValue && user.CodeLockoutUntil.Value > DateTime.UtcNow)
            {
                var remainingMinutes = (int)(user.CodeLockoutUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
                throw new ForbiddenException($"Account is temporarily locked due to too many failed attempts. Please try again in {remainingMinutes} minute(s).");
            }

            // Check if code has expired
            if (!user.VerificationCodeExpiry.HasValue || user.VerificationCodeExpiry.Value < DateTime.UtcNow)
            {
                throw new UnauthorizedException(LocalizationKeys.Otp.CodeExpired);
            }

            // Verify code matches database
            if (string.IsNullOrEmpty(user.VerificationCode) || 
                user.VerificationCode != request.VerificationCode)
            {
                // Increment failed attempts
                user.FailedCodeAttempts += 1;
                
                // Check if max attempts reached
                if (user.FailedCodeAttempts >= otpSettings.MaxAttempts)
                {
                    user.CodeLockoutUntil = DateTime.UtcNow.AddMinutes(otpSettings.LockoutMinutes);
                    await _userRepository.UpdateAsync(user, cancellationToken);
                    throw new ForbiddenException($"Too many failed attempts. Account is locked for {otpSettings.LockoutMinutes} minute(s).");
                }
                
                user.LastModified = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user, cancellationToken);
                
                var remainingAttempts = otpSettings.MaxAttempts - user.FailedCodeAttempts;
                throw new UnauthorizedException($"Email or verification code is incorrect. {remainingAttempts} attempt(s) remaining.");
            }

            // Clear verification code and reset security fields after successful login
            user.VerificationCode = null;
            user.VerificationCodeExpiry = null;
            user.FailedCodeAttempts = 0;
            user.CodeLockoutUntil = null;
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
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }

    private OtpSettings GetOtpSettings()
    {
        var multiTenancyEnabled = _configuration.GetValue<bool>("MultiTenancy:Enabled", false);

        if (multiTenancyEnabled && _tenantContext.HasTenant && 
            _tenantContext.CurrentTenant?.Configuration?.Otp != null)
        {
            // Use tenant-specific OTP settings
            return _tenantContext.CurrentTenant.Configuration.Otp;
        }

        // Fallback to appsettings.json
        return _configuration.GetSection("OtpSettings").Get<OtpSettings>() ?? new OtpSettings();
    }
}
