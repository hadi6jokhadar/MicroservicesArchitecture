namespace IhsanDev.Shared.Infrastructure.Services.Notification;

/// <summary>
/// Interface for sending notifications to the Notification Service
/// This provides service-to-service communication capabilities for sending notifications
/// </summary>
public interface INotificationServiceClient
{
    /// <summary>
    /// Send a notification to a specific user in a tenant
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    /// <param name="userId">The user identifier</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="data">Optional JSON data payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if notification was sent successfully, false otherwise</returns>
    Task<bool> SendNotificationAsync(
        string tenantId,
        int userId,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a broadcast notification to all users in a tenant
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="data">Optional JSON data payload</param>
    /// <param name="deliveryType">"Both" (SignalR + Firebase), "SignalR", or "Firebase" — use "SignalR" for
    /// high-frequency internal progress updates that shouldn't trigger a mobile push per event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if notification was sent successfully, false otherwise</returns>
    Task<bool> SendTenantBroadcastAsync(
        string tenantId,
        string title,
        string message,
        string? data = null,
        string deliveryType = "Both",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a global notification to all users across all tenants
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="data">Optional JSON data payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if notification was sent successfully, false otherwise</returns>
    Task<bool> SendGlobalNotificationAsync(
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default);
}
