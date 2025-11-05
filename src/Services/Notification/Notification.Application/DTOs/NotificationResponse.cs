using AutoMapper;
using IhsanDev.Shared.Application.Common.Mappings;

namespace Notification.Application.DTOs;

/// <summary>
/// Response DTO for notification details
/// </summary>
public class NotificationResponse : IMapFrom<Domain.Entities.Notification>
{
    /// <summary>
    /// Notification unique identifier
    /// </summary>
    public int Id { get; set; }

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
    public bool IsRead { get; set; }

    /// <summary>
    /// Timestamp when notification was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when notification was read
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// User ID (null for broadcast notifications)
    /// </summary>
    public int? UserId { get; set; }

    public void Mapping(Profile profile)
    {
        profile.CreateMap<Domain.Entities.Notification, NotificationResponse>()
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.Created));
    }
}
