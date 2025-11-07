using AutoMapper;
using IhsanDev.Shared.Application.Common.Mappings;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Application.DTOs;

/// <summary>
/// DTO for notification queue item details (SuperAdmin view)
/// </summary>
public class QueueItemDto : IMapFrom<NotificationQueueItem>
{
    public int Id { get; set; }
    public string? TenantId { get; set; }
    public int? UserId { get; set; }
    public DeliveryType DeliveryType { get; set; }
    public Priority Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Data { get; set; }
    public QueueStatus QueueStatus { get; set; }
    public int RetryCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Error { get; set; }
    public int? NotificationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public void Mapping(Profile profile)
    {
        profile.CreateMap<NotificationQueueItem, QueueItemDto>()
            .ForMember(d => d.CreatedAt, opt => opt.MapFrom(s => s.Created))
            .ForMember(d => d.UpdatedAt, opt => opt.MapFrom(s => s.LastModified));
    }
}
