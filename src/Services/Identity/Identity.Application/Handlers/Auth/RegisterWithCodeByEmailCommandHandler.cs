using Identity.Application.Commands.Auth;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Otp;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Identity.Application.Handlers.Auth;

public class RegisterWithCodeByEmailCommandHandler : IRequestHandler<RegisterWithCodeByEmailCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IOtpService _otpService;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;

    public RegisterWithCodeByEmailCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IOtpService otpService,
        IConfiguration configuration,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _otpService = otpService;
        _configuration = configuration;
        _tenantContext = tenantContext;
    }

    public async Task<bool> Handle(RegisterWithCodeByEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get OTP settings from tenant or appsettings
            var otpSettings = GetOtpSettings();

            // Check if email exists
            bool emailExists = await _userRepository.EmailExistsAsync(request.Email, cancellationToken);
            if (emailExists)
            {
                throw new ConflictException(LocalizationKeys.Exceptions.EmailAlreadyExists);
            }

            // Generate verification code with settings
            var verificationCode = _otpService.GenerateCode(otpSettings);

            // Calculate expiry time
            var expiryTime = DateTime.UtcNow.AddSeconds(otpSettings.ExpirationSeconds);

            // Create user without password and without phone
            var user = new User
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = null, // No phone required for email registration
                Data = request.Data,
                VerificationCode = verificationCode,
                VerificationCodeExpiry = expiryTime,
                LastCodeSentAt = DateTime.UtcNow,
                FailedCodeAttempts = 0,
                Created = DateTime.UtcNow,
                Status = true,
                PasswordHash = null // No password for code-based registration
            };

            await _userRepository.AddAsync(user, cancellationToken);

            // Assign default "User" role
            var userRole = await _roleRepository.GetByNameAsync("User", cancellationToken);
            if (userRole != null)
            {
                await _userRoleRepository.AssignRolesToUserAsync(user.Id, [userRole.Id], cancellationToken);
            }

            // TODO: Send verification code via Email
            // For now, the code is just saved to the database
            // In production, you would send it via Email using an external provider

            return true;
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
