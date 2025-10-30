using System.Security.Cryptography;
using System.Text;
using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace IhsanDev.Shared.Infrastructure.Services.Otp;

/// <summary>
/// Default implementation of IOtpService with internal random number generation
/// Supports external OTP provider integration
/// </summary>
public class OtpService : IOtpService
{
    private readonly IExternalOtpProvider? _externalOtpProvider;
    private const string AlphanumericChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excluding similar looking chars

    public OtpService(IExternalOtpProvider? externalOtpProvider = null)
    {
        _externalOtpProvider = externalOtpProvider;
    }

    /// <summary>
    /// Generates a cryptographically secure random code based on settings
    /// </summary>
    public string GenerateCode(OtpSettings? settings = null)
    {
        // Use default settings if not provided
        var length = settings?.CodeLength ?? 6;
        var useAlphanumeric = settings?.UseAlphanumeric ?? false;

        if (length < 4 || length > 10)
        {
            throw new ArgumentException("Code length must be between 4 and 10 digits", nameof(length));
        }

        if (useAlphanumeric)
        {
            return GenerateAlphanumericCode(length);
        }
        else
        {
            return GenerateNumericCode(length);
        }
    }

    /// <summary>
    /// Generates a numeric-only code
    /// </summary>
    private string GenerateNumericCode(int length)
    {
        // Generate cryptographically secure random number
        var randomNumber = RandomNumberGenerator.GetInt32(
            (int)Math.Pow(10, length - 1), 
            (int)Math.Pow(10, length)
        );

        return randomNumber.ToString();
    }

    /// <summary>
    /// Generates an alphanumeric code
    /// </summary>
    private string GenerateAlphanumericCode(int length)
    {
        var code = new StringBuilder(length);
        var bytes = new byte[length];
        
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        foreach (var b in bytes)
        {
            code.Append(AlphanumericChars[b % AlphanumericChars.Length]);
        }

        return code.ToString();
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
