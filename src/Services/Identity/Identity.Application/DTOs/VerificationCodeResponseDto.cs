namespace Identity.Application.DTOs;

/// <summary>
/// Response DTO for verification code operations
/// In development mode: returns the verification code as a string for testing
/// In production mode: returns success as boolean
/// </summary>
public class VerificationCodeResponseDto
{
    /// <summary>
    /// Success indicator (always present)
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Verification code (only present in development mode)
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Message describing the result
    /// </summary>
    public string? Message { get; set; }
}
