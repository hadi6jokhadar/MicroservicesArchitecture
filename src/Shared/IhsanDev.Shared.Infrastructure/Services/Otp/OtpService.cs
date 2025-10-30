using System.Security.Cryptography;

namespace IhsanDev.Shared.Infrastructure.Services.Otp;

/// <summary>
/// Default implementation of IOtpService with internal random number generation
/// Supports external OTP provider integration
/// </summary>
public class OtpService : IOtpService
{
    private readonly IExternalOtpProvider? _externalOtpProvider;

    public OtpService(IExternalOtpProvider? externalOtpProvider = null)
    {
        _externalOtpProvider = externalOtpProvider;
    }

    /// <summary>
    /// Generates a cryptographically secure random numeric code
    /// </summary>
    public string GenerateCode(int length = 5)
    {
        if (length < 4 || length > 10)
        {
            throw new ArgumentException("Code length must be between 4 and 10 digits", nameof(length));
        }

        // Generate cryptographically secure random number
        var randomNumber = RandomNumberGenerator.GetInt32(
            (int)Math.Pow(10, length - 1), 
            (int)Math.Pow(10, length)
        );

        return randomNumber.ToString();
    }

    /// <summary>
    /// Generates verification code using external OTP provider if configured
    /// Falls back to internal generation if no provider is configured
    /// </summary>
    public async Task<string> GenerateCodeWithExternalProviderAsync(
        string phoneNumber, 
        CancellationToken cancellationToken = default)
    {
        if (_externalOtpProvider != null)
        {
            return await _externalOtpProvider.SendOtpAsync(phoneNumber, cancellationToken);
        }

        // Fallback to internal generation
        return GenerateCode();
    }
}
