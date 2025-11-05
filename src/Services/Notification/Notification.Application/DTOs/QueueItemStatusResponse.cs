using AutoMapper;
using IhsanDev.Shared.Application.Common.Mappings;
using Notification.Domain.Entities;

namespace Notification.Application.DTOs;

/// <summary>
/// Response DTO for notification queue item status
/// </summary>
public class QueueItemStatusResponse : IMapFrom<NotificationQueueItem>
{
    /// <summary>
    /// Queue item unique identifier
    /// </summary>
    public int QueueItemId { get; set; }

    /// <summary>
    /// Current processing status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of delivery attempts
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Timestamp when processed
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Tenant notification ID (if persisted)
    /// </summary>
    public int? NotificationId { get; set; }

    /// <summary>
    /// Timestamp when created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    public void Mapping(Profile profile)
    {
        profile.CreateMap<NotificationQueueItem, QueueItemStatusResponse>()
            .ForMember(dest => dest.QueueItemId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.QueueStatus.ToString()))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.Created));
    }
}
