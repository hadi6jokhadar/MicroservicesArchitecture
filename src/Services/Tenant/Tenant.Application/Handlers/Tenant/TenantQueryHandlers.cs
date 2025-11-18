using IhsanDev.Shared.Application.Common.Mappings;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    public GetTenantConfigQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
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

            return TenantConfigDto.MapFrom(tenant);
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}

/// <summary>
/// Handler for getting tenant by ID (excludes Data field)
/// </summary>
public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, TenantDto?>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantByIdQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
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

            return TenantDto.MapFrom(tenant);
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}

/// <summary>
/// Handler for getting tenant by user ID
/// </summary>
public class GetTenantByUserQueryHandler : IRequestHandler<GetTenantByUserQuery, TenantDto?>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantByUserQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
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

            return TenantDto.MapFrom(tenant);
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}

/// <summary>
/// Handler for getting all active tenants with pagination
/// </summary>
public class GetAllActiveTenantsQueryHandler : IRequestHandler<GetAllActiveTenantsQuery, PaginatedList<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetAllActiveTenantsQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
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

            // Manual projection to DTO
            var dtoQuery = query.Select(t => new TenantDto
            {
                Id = t.Id,
                TenantId = t.TenantId,
                TenantName = t.TenantName,
                UserId = t.UserId,
                StartDate = t.StartDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                ExpireDate = t.ExpireDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                IsActive = t.IsActive,
                IsExpired = t.IsExpired,
                Created = t.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                LastModified = t.LastModified != null ? t.LastModified.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) : null
            });

            var paginatedList = await dtoQuery
                .PaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);

            return paginatedList;
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}

/// <summary>
/// Handler for getting all active tenants with configuration (includes Data field)
/// Uses caching to reduce database load for background jobs and service-to-service calls
/// </summary>
public class GetAllActiveTenantsWithConfigQueryHandler : IRequestHandler<GetAllActiveTenantsWithConfigQuery, PaginatedList<TenantConfigDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetAllActiveTenantsWithConfigQueryHandler> _logger;

    public GetAllActiveTenantsWithConfigQueryHandler(
        ITenantRepository tenantRepository,
        ICacheService cacheService,
        ILogger<GetAllActiveTenantsWithConfigQueryHandler> logger)
    {
        _tenantRepository = tenantRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<PaginatedList<TenantConfigDto>> Handle(GetAllActiveTenantsWithConfigQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Cache key includes pagination parameters
            var cacheKey = $"all_active_tenants_with_config_page_{request.PageNumber}_size_{request.PageSize}";

            // Try to get from cache first
            var cachedResult = await _cacheService.GetAsync<PaginatedList<TenantConfigDto>>(cacheKey, cancellationToken);
            if (cachedResult != null)
            {
                _logger.LogDebug(
                    "Cache HIT for all active tenants with config (Page: {PageNumber}, Size: {PageSize})",
                    request.PageNumber,
                    request.PageSize);
                return cachedResult;
            }

            _logger.LogDebug(
                "Cache MISS for all active tenants with config (Page: {PageNumber}, Size: {PageSize}). Fetching from database...",
                request.PageNumber,
                request.PageSize);

            var query = _tenantRepository.GetAll();

            // Filter only active tenants
            query = query.Where(t => t.IsActive && !t.IsArchived);

            // Order by created date (newest first)
            query = query.OrderByDescending(t => t.Created);

            // Get total count for pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination and convert to list
            var tenants = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                query
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize),
                cancellationToken);

            // Map to DTOs with config (in-memory operation after database fetch)
            var tenantConfigDtos = tenants
                .Select(TenantConfigDto.MapFrom)
                .ToList();

            // Calculate total pages
            var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

            // Create paginated result using public constructor
            var paginatedList = new PaginatedList<TenantConfigDto>(
                tenantConfigDtos,
                totalCount,
                request.PageNumber,
                totalPages);

            // Cache for 7 days - Cache is automatically invalidated when tenants are created/updated/deleted
            // TenantCacheRefreshService also refreshes individual tenant configs every hour
            await _cacheService.SetAsync(cacheKey, paginatedList, TimeSpan.FromDays(7), cancellationToken);

            _logger.LogInformation(
                "Cached all active tenants with config (Page: {PageNumber}, Size: {PageSize}, Total: {TotalCount}) for 7 days",
                request.PageNumber,
                request.PageSize,
                totalCount);

            return paginatedList;
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
