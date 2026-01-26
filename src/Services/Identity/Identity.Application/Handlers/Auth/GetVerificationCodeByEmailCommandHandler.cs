using Identity.Application.Commands.Auth;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Otp;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Identity.Application.Handlers.Auth;

public class GetVerificationCodeByEmailCommandHandler : IRequestHandler<GetVerificationCodeByEmailCommand, VerificationCodeResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly IHostEnvironment _hostEnvironment;

    public GetVerificationCodeByEmailCommandHandler(
        IUserRepository userRepository,
        IOtpService otpService,
        IConfiguration configuration,
        ITenantContext tenantContext,
        IHostEnvironment hostEnvironment)
    {
        _userRepository = userRepository;
        _otpService = otpService;
        _configuration = configuration;
        _tenantContext = tenantContext;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<VerificationCodeResponseDto> Handle(GetVerificationCodeByEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get OTP settings from tenant or appsettings
            var otpSettings = GetOtpSettings();

            // Check if email exists
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (user == null)
            {
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);
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

            // Check resend cooldown
            if (user.LastCodeSentAt.HasValue)
            {
                var timeSinceLastCode = (DateTime.UtcNow - user.LastCodeSentAt.Value).TotalSeconds;
                if (timeSinceLastCode < otpSettings.ResendCooldownSeconds)
                {
                    var remainingSeconds = (int)(otpSettings.ResendCooldownSeconds - timeSinceLastCode);
                    throw new BadRequestException($"Please wait {remainingSeconds} second(s) before requesting a new code.");
                }
            }

            // Generate verification code with settings
            var verificationCode = _otpService.GenerateCode(otpSettings);

            // Calculate expiry time
            var expiryTime = DateTime.UtcNow.AddSeconds(otpSettings.ExpirationSeconds);

            // Save code to user entity
            user.VerificationCode = verificationCode;
            user.VerificationCodeExpiry = expiryTime;
            user.LastCodeSentAt = DateTime.UtcNow;
            user.FailedCodeAttempts = 0; // Reset failed attempts when new code is sent
            user.LastModified = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);

            // TODO: Send code via Email
            // For now, the code is just saved to the database
            // In production, you would send it via Email using an external provider

            // Return response with code in development mode, without code in production
            return new VerificationCodeResponseDto
            {
                Success = true,
                Code = _hostEnvironment.IsDevelopment() ? verificationCode : null
            };
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
