namespace IhsanDev.Shared.Infrastructure.Services.Otp;

/// <summary>
/// Interface for external OTP providers (e.g., Twilio, AWS SNS, Azure Communication Services)
/// Implement this interface to integrate with external SMS/OTP services
/// </summary>
public interface IExternalOtpProvider
{
    /// <summary>
    /// Sends OTP via external provider
    /// </summary>
    /// <param name="phoneNumber">Phone number to send OTP to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated OTP code that was sent</returns>
    Task<string> SendOtpAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
