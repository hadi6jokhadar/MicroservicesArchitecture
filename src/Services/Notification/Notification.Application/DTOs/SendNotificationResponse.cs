using AutoMapper;
using IhsanDev.Shared.Application.Common.Mappings;
using Notification.Domain.Entities;

namespace Notification.Application.DTOs;

/// <summary>
/// Response DTO after sending notification
/// </summary>
public class SendNotificationResponse : IMapFrom<NotificationQueueItem>
{
    /// <summary>
    /// Queue item unique identifier
    /// </summary>
    public int QueueItemId { get; set; }

    /// <summary>
    /// Current status of the notification
    /// </summary>
    public string Status { get; set; } = "Queued";

    /// <summary>
    /// Timestamp when notification was queued
    /// </summary>
    public DateTime QueuedAt { get; set; }

    /// <summary>
    /// Priority level
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Delivery channels configured
    /// </summary>
    public string? DeliveryType { get; set; }

    public void Mapping(Profile profile)
    {
        profile.CreateMap<NotificationQueueItem, SendNotificationResponse>()
            .ForMember(dest => dest.QueueItemId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.QueueStatus.ToString()))
            .ForMember(dest => dest.QueuedAt, opt => opt.MapFrom(src => src.Created))
            .ForMember(dest => dest.Priority, opt => opt.MapFrom(src => src.Priority.ToString()))
            .ForMember(dest => dest.DeliveryType, opt => opt.MapFrom(src => src.DeliveryType.ToString()));
    }
}
