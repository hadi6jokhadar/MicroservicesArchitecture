namespace Notification.Application.DTOs;

/// <summary>
/// Result of Firebase multicast notification send operation
/// </summary>
public class FirebaseMulticastResult
{
    /// <summary>
    /// Number of successfully sent notifications
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed notifications
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// List of invalid or unregistered device token IDs that should be deleted
    /// </summary>
    public List<string> InvalidTokenIds { get; set; } = new();

    /// <summary>
    /// Detailed results per token (token -> success/failure)
    /// </summary>
    public Dictionary<string, bool> TokenResults { get; set; } = new();
}
