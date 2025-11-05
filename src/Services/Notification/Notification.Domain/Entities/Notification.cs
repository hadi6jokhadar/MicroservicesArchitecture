using System.ComponentModel.DataAnnotations;
using IhsanDev.Shared.Kernel.Entities;

namespace Notification.Domain.Entities;

/// <summary>
/// Notification record stored per tenant
/// Stored in Tenant DBs (each tenant's database)
/// </summary>
public class Notification : BaseEntity
{

    /// <summary>
    /// User ID (null = notification for all users in tenant)
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Notification title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Notification message body
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Additional JSON payload data
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// Whether notification has been read
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Timestamp when notification was read
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Reference back to queue item (for tracking)
    /// </summary>
    public int? QueueItemId { get; set; }
}
