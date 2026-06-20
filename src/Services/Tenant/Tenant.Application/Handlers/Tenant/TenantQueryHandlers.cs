using IhsanDev.Shared.Application.Common.Mappings;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Constants;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
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
    private readonly ILogger<GetTenantConfigQueryHandler> _logger;

    public GetTenantConfigQueryHandler(
        ITenantRepository tenantRepository,
        ILogger<GetTenantConfigQueryHandler> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting config for tenant {TenantId}", request.TenantId);
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
    private readonly ILogger<GetTenantByIdQueryHandler> _logger;

    public GetTenantByIdQueryHandler(
        ITenantRepository tenantRepository,
        ILogger<GetTenantByIdQueryHandler> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting tenant by ID {TenantId}", request.TenantId);
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
    private readonly ILogger<GetTenantByUserQueryHandler> _logger;

    public GetTenantByUserQueryHandler(
        ITenantRepository tenantRepository,
        ILogger<GetTenantByUserQueryHandler> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting tenant by user ID {UserId}", request.UserId);
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
    private readonly ILogger<GetAllActiveTenantsQueryHandler> _logger;

    public GetAllActiveTenantsQueryHandler(
        ITenantRepository tenantRepository,
        ILogger<GetAllActiveTenantsQueryHandler> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    public async Task<PaginatedList<TenantDto>> Handle(GetAllActiveTenantsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Use repository method that supports isArchived filtering
            var (items, totalCount) = await _tenantRepository.GetAllActiveAsync(
                request.PageNumber,
                request.PageSize,
                request.IsArchived,
                cancellationToken);

            var tenantDtos = items.Select(t => new TenantDto
            {
                Id = t.Id,
                TenantId = t.TenantId,
                TenantName = t.TenantName,
                UserId = t.UserId,
                StartDate = t.StartDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                ExpireDate = t.ExpireDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                IsActive = t.IsActive,
                IsExpired = t.IsExpired,
                IsArchived = t.IsArchived,
                Created = t.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                LastModified = t.LastModified != null ? t.LastModified.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) : null
            }).ToList();

            var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

            return new PaginatedList<TenantDto>(
                tenantDtos,
                totalCount,
                request.PageNumber,
                totalPages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting all active tenants");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting all active tenants with config");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}

/// <summary>
/// Returns feature flags for a tenant, merged with system defaults.
/// When no tenantId is provided, returns the default flag set.
/// Never throws — falls back to defaults on any error.
/// </summary>
public class GetTenantFeatureFlagsQueryHandler : IRequestHandler<GetTenantFeatureFlagsQuery, Dictionary<string, bool>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetTenantFeatureFlagsQueryHandler> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public GetTenantFeatureFlagsQueryHandler(
        ITenantRepository tenantRepository,
        ICacheService cacheService,
        ILogger<GetTenantFeatureFlagsQueryHandler> logger)
    {
        _tenantRepository = tenantRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Dictionary<string, bool>> Handle(GetTenantFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var defaults = GetDefaultFlags();

        if (string.IsNullOrWhiteSpace(request.TenantId))
            return defaults;

        var cacheKey = $"tenant_feature_flags_{request.TenantId}";

        var cached = await _cacheService.GetAsync<Dictionary<string, bool>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Cache HIT for feature flags of tenant {TenantId}", request.TenantId);
            return cached;
        }

        try
        {
            var tenant = await _tenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
            if (tenant == null)
                return defaults;

            var tenantFlags = DeserializeFeatureFlags(tenant.Data);

            Dictionary<string, bool> result;
            if (tenantFlags == null || tenantFlags.Count == 0)
            {
                result = defaults;
            }
            else
            {
                // Tenant values override defaults; unknown keys from the tenant are included as-is.
                result = new Dictionary<string, bool>(defaults);
                foreach (var (key, value) in tenantFlags)
                    result[key] = value;
            }

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromDays(7), cancellationToken);
            _logger.LogDebug("Cached feature flags for tenant {TenantId} for 7 days", request.TenantId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load feature flags for tenant {TenantId}, returning defaults", request.TenantId);
            return defaults;
        }
    }

    private static Dictionary<string, bool> GetDefaultFlags() => new()
    {
        [FeatureFlags.AiChatEnabled] = true,
        [FeatureFlags.NasheedIngestionEnabled] = true,
        [FeatureFlags.IsBackgroundJobPageEnabled] = true,
        [FeatureFlags.IsAuditLogPageEnabled] = true,
    };

    private static Dictionary<string, bool>? DeserializeFeatureFlags(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        try
        {
            return JsonSerializer.Deserialize<TenantConfiguration>(data, _jsonOptions)?.FeatureFlags;
        }
        catch
        {
            return null;
        }
    }
}
