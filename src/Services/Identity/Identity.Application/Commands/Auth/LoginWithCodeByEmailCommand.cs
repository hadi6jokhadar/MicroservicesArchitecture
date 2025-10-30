using FluentValidation;
using Identity.Application.DTOs;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Identity.Application.Commands.Auth;

public record LoginWithCodeByEmailCommand(
    string Email,
    string VerificationCode
) : IRequest<UserDtoIncludesToken>;

public class LoginWithCodeByEmailCommandValidator : AbstractValidator<LoginWithCodeByEmailCommand>
{
    public LoginWithCodeByEmailCommandValidator(IConfiguration configuration, ITenantContext tenantContext)
    {
        // Get OTP settings from tenant configuration if multi-tenancy is enabled, otherwise from appsettings
        var otpSettings = GetOtpSettings(configuration, tenantContext);
        var codeLength = otpSettings.CodeLength;
        var useAlphanumeric = otpSettings.UseAlphanumeric;

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Valid email address is required");

        RuleFor(x => x.VerificationCode)
            .NotEmpty().WithMessage("Verification code is required")
            .Length(codeLength).WithMessage($"Verification code must be {codeLength} characters")
            .Must(code => useAlphanumeric ? code.All(char.IsLetterOrDigit) : code.All(char.IsDigit))
            .WithMessage(useAlphanumeric 
                ? $"Verification code must contain only letters and digits" 
                : $"Verification code must contain only digits");
    }

    private static OtpSettings GetOtpSettings(IConfiguration configuration, ITenantContext tenantContext)
    {
        // Check if multi-tenancy is enabled and tenant has OTP settings
        if (tenantContext.IsMultiTenantMode && 
            tenantContext.HasTenant && 
            tenantContext.CurrentTenant?.Configuration?.Otp != null)
        {
            return tenantContext.CurrentTenant.Configuration.Otp;
        }

        // Fall back to appsettings.json
        return configuration.GetSection("OtpSettings").Get<OtpSettings>() ?? new OtpSettings();
    }
}
