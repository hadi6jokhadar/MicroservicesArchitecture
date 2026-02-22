using IhsanDev.Shared.Application.Common.Models;
using MediatR;
using Notification.Application.Commands;
using Notification.Application.DTOs;
using Notification.Domain.Repositories;

namespace Notification.Application.Handlers;

/// <summary>
/// Handler for GetQueueItemsCommand
/// Returns paginated list of queue items with filtering (SuperAdmin only)
/// </summary>
public class GetQueueItemsQueryHandler : IRequestHandler<GetQueueItemsCommand, PaginatedList<QueueItemDto>>
{
    private readonly INotificationQueueRepository _queueRepository;

    public GetQueueItemsQueryHandler(
        INotificationQueueRepository queueRepository)
    {
        _queueRepository = queueRepository;
    }

    public async Task<PaginatedList<QueueItemDto>> Handle(
        GetQueueItemsCommand request,
        CancellationToken cancellationToken)
    {
        // Get filtered queryable
        var query = _queueRepository.GetFilteredQueryable(
            tenantId: request.TenantId,
            userId: request.UserId,
            status: request.Status,
            priority: request.Priority,
            deliveryType: request.DeliveryType,
            fromDate: request.FromDate,
            toDate: request.ToDate,
            searchTerm: request.SearchTerm,
            isArchived: request.IsArchived
        );

        // Manual projection to DTOs
        var dtoQuery = query.Select(q => new QueueItemDto
        {
            Id = q.Id,
            TenantId = q.TenantId,
            UserId = q.UserId,
            DeliveryType = q.DeliveryType,
            Priority = q.Priority,
            Title = q.Title,
            Message = q.Message,
            Data = q.Data,
            QueueStatus = q.QueueStatus,
            RetryCount = q.RetryCount,
            ProcessedAt = q.ProcessedAt != null ? q.ProcessedAt.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) : null,
            ExpiresAt = q.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            Error = q.Error,
            NotificationId = q.NotificationId,
            Created = q.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            LastModified = q.LastModified != null ? q.LastModified.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) : null,
            IsArchived = q.IsArchived
        });

        // Create paginated result
        return await PaginatedList<QueueItemDto>.CreateAsync(
            dtoQuery,
            request.PageNumber,
            request.PageSize,
            cancellationToken
        );
    }
}
