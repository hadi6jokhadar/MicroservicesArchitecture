using MediatR;
using Notification.Application.DTOs;

namespace Notification.Application.Commands;

/// <summary>
/// Command to toggle queue item archived status
/// </summary>
public record ToggleQueueItemArchivedStatusCommand(int Id) : IRequest<QueueItemDto>;
