namespace Notification.Domain.Enums;

/// <summary>
/// Priority level for notification processing
/// </summary>
public enum Priority
{
    /// <summary>
    /// Process in background, can be delayed
    /// </summary>
    Waitable = 0,
    
    /// <summary>
    /// Process immediately
    /// </summary>
    Immediate = 1
}
