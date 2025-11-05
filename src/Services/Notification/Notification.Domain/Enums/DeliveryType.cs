namespace Notification.Domain.Enums;

/// <summary>
/// Delivery type for notifications
/// </summary>
public enum DeliveryType
{
    /// <summary>
    /// Real-time delivery via SignalR
    /// </summary>
    SignalR = 1,
    
    /// <summary>
    /// Push notification via Firebase
    /// </summary>
    Firebase = 2,
    
    /// <summary>
    /// Both SignalR and Firebase
    /// </summary>
    Both = 3
}
