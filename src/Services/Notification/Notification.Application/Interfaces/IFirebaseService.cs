using Notification.Application.DTOs;

namespace Notification.Application.Interfaces;

/// <summary>
/// Service for sending Firebase Cloud Messaging notifications
/// </summary>
public interface IFirebaseService
{
    /// <summary>
    /// Send push notification to a specific device token
    /// </summary>
    /// <param name="deviceToken">FCM device token</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="data">Additional data payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if notification sent successfully, false otherwise</returns>
    Task<bool> SendToDeviceAsync(
        string deviceToken, 
        string title, 
        string message, 
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send push notification to multiple device tokens
    /// </summary>
    /// <param name="deviceTokens">List of FCM device tokens</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="data">Additional data payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Firebase multicast result with success/failure counts and invalid tokens</returns>
    Task<FirebaseMulticastResult> SendToMultipleDevicesAsync(
        List<string> deviceTokens,
        string title,
        string message,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate if Firebase is enabled and initialized
    /// </summary>
    bool IsEnabled { get; }
}
