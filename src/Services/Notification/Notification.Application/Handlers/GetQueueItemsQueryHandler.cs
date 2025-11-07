using AutoMapper;
using AutoMapper.QueryableExtensions;
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
    private readonly IMapper _mapper;

    public GetQueueItemsQueryHandler(
        INotificationQueueRepository queueRepository,
        IMapper mapper)
    {
        _queueRepository = queueRepository;
        _mapper = mapper;
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
            searchTerm: request.SearchTerm
        );

        // Project to DTOs and paginate
        var dtoQuery = query.ProjectTo<QueueItemDto>(_mapper.ConfigurationProvider);

        // Create paginated result
        return await PaginatedList<QueueItemDto>.CreateAsync(
            dtoQuery,
            request.PageNumber,
            request.PageSize,
            cancellationToken
        );
    }
}
