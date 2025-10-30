namespace IhsanDev.Shared.Infrastructure.Services.Otp;

/// <summary>
/// Service for generating and validating One-Time Passwords (OTP)
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a verification code
    /// </summary>
    /// <param name="length">Length of the verification code (default: 5)</param>
    /// <returns>Generated verification code</returns>
    string GenerateCode(int length = 5);

    /// <summary>
    /// Generates a verification code using an external OTP provider
    /// </summary>
    /// <param name="phoneNumber">Phone number to send OTP to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated verification code</returns>
    Task<string> GenerateCodeWithExternalProviderAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
