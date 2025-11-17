using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using MediatR;
using System.Text.Json;
using Tenant.Application.Commands.Tenant;
using Tenant.Application.DTOs;
using Tenant.Domain.Repositories;

namespace Tenant.Application.Handlers.Tenant;

/// <summary>
/// Handler for updating tenant settings
/// </summary>
public class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICacheService _cacheService;

    public UpdateTenantCommandHandler(ITenantRepository tenantRepository, ICacheService cacheService)
    {
        _tenantRepository = tenantRepository;
        _cacheService = cacheService;
    }

    public async Task<TenantDto> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
            if (tenant == null)
            {
                throw new NotFoundException(LocalizationKeys.Exceptions.TenantNotFound);
            }

            // Serialize TenantConfiguration to JSON string for database storage
            var dataJson = JsonSerializer.Serialize(request.Data, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Update tenant properties
            tenant.TenantName = request.TenantName;
            tenant.StartDate = request.StartDate;
            tenant.ExpireDate = request.ExpireDate;
            tenant.Data = dataJson;
            tenant.IsActive = request.IsActive;
            tenant.LastModified = DateTime.UtcNow;

            await _tenantRepository.UpdateAsync(tenant, cancellationToken);

            // Invalidate cache for this tenant so it gets refreshed on next request
            var cacheKey = $"tenant_config_{tenant.TenantId}";
            await _cacheService.RemoveAsync(cacheKey, cancellationToken);

            // Invalidate paginated tenant list cache (tenant data changed)
            await _cacheService.RemoveByPatternAsync("all_active_tenants_with_config_*", cancellationToken);

            return TenantDto.MapFrom(tenant);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
