namespace Notification.Domain.Enums;

/// <summary>
/// Status of notification in the queue
/// </summary>
public enum QueueStatus
{
    /// <summary>
    /// Queued and waiting to be processed
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Currently being processed
    /// </summary>
    Processing = 1,
    
    /// <summary>
    /// Successfully sent
    /// </summary>
    Sent = 2,
    
    /// <summary>
    /// Failed to send after retries
    /// </summary>
    Failed = 3,
    
    /// <summary>
    /// Expired before delivery
    /// </summary>
    Expired = 4
}
