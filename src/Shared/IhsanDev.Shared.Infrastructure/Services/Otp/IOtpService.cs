using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace IhsanDev.Shared.Infrastructure.Services.Otp;

/// <summary>
/// Service for generating and validating One-Time Passwords (OTP)
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a verification code with configurable settings
    /// </summary>
    /// <param name="settings">OTP configuration settings (uses defaults if null)</param>
    /// <returns>Generated verification code</returns>
    string GenerateCode(OtpSettings? settings = null);

    /// <summary>
    /// Generates a verification code using an external OTP provider
    /// </summary>
    /// <param name="phoneNumber">Phone number to send OTP to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated verification code</returns>
    Task<string> GenerateCodeWithExternalProviderAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
