using AutoMapper;
using AutoMapper.QueryableExtensions;
using IhsanDev.Shared.Application.Common.Mappings;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Tenant.Application.Commands.Tenant;
using Tenant.Application.DTOs;
using Tenant.Domain.Repositories;

namespace Tenant.Application.Handlers.Tenant;

/// <summary>
/// Handler for getting tenant configuration (includes Data field)
/// </summary>
public class GetTenantConfigQueryHandler : IRequestHandler<GetTenantConfigQuery, TenantConfigDto?>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IMapper _mapper;

    public GetTenantConfigQueryHandler(ITenantRepository tenantRepository, IMapper mapper)
    {
        _tenantRepository = tenantRepository;
        _mapper = mapper;
    }

    public async Task<TenantConfigDto?> Handle(GetTenantConfigQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
            if (tenant == null)
            {
                return null;
            }

            return _mapper.Map<TenantConfigDto>(tenant);
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to get tenant configuration: " + ex.Message);
        }
    }
}

/// <summary>
/// Handler for getting tenant by ID (excludes Data field)
/// </summary>
public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, TenantDto?>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IMapper _mapper;

    public GetTenantByIdQueryHandler(ITenantRepository tenantRepository, IMapper mapper)
    {
        _tenantRepository = tenantRepository;
        _mapper = mapper;
    }

    public async Task<TenantDto?> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
            if (tenant == null)
            {
                return null;
            }

            return _mapper.Map<TenantDto>(tenant);
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to get tenant: " + ex.Message);
        }
    }
}

/// <summary>
/// Handler for getting tenant by user ID
/// </summary>
public class GetTenantByUserQueryHandler : IRequestHandler<GetTenantByUserQuery, TenantDto?>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IMapper _mapper;

    public GetTenantByUserQueryHandler(ITenantRepository tenantRepository, IMapper mapper)
    {
        _tenantRepository = tenantRepository;
        _mapper = mapper;
    }

    public async Task<TenantDto?> Handle(GetTenantByUserQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (tenant == null)
            {
                return null;
            }

            return _mapper.Map<TenantDto>(tenant);
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to get tenant by user: " + ex.Message);
        }
    }
}

/// <summary>
/// Handler for getting all active tenants with pagination
/// </summary>
public class GetAllActiveTenantsQueryHandler : IRequestHandler<GetAllActiveTenantsQuery, PaginatedList<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IMapper _mapper;

    public GetAllActiveTenantsQueryHandler(ITenantRepository tenantRepository, IMapper mapper)
    {
        _tenantRepository = tenantRepository;
        _mapper = mapper;
    }

    public async Task<PaginatedList<TenantDto>> Handle(GetAllActiveTenantsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _tenantRepository.GetAll();

            // Filter only active tenants
            query = query.Where(t => t.IsActive && !t.IsArchived);

            // Order by created date (newest first)
            query = query.OrderByDescending(t => t.Created);

            // Use AutoMapper's ProjectTo for efficient mapping and pagination
            var paginatedList = await query
                .ProjectTo<TenantDto>(_mapper.ConfigurationProvider)
                .PaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);

            return paginatedList;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to get active tenants: " + ex.Message);
        }
    }
}
