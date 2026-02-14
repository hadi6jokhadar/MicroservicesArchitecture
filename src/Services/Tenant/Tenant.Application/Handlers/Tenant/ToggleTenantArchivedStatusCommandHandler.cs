using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using MediatR;
using Tenant.Application.Commands.Tenant;
using Tenant.Application.DTOs;
using Tenant.Domain.Repositories;

namespace Tenant.Application.Handlers.Tenant;

/// <summary>
/// Handler for toggling tenant archived status
/// </summary>
public class ToggleTenantArchivedStatusCommandHandler : IRequestHandler<ToggleTenantArchivedStatusCommand, TenantDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICacheService _cacheService;

    public ToggleTenantArchivedStatusCommandHandler(ITenantRepository tenantRepository, ICacheService cacheService)
    {
        _tenantRepository = tenantRepository;
        _cacheService = cacheService;
    }

    public async Task<TenantDto> Handle(ToggleTenantArchivedStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
            if (tenant == null)
            {
                throw new NotFoundException(LocalizationKeys.Exceptions.TenantNotFound);
            }

            tenant.IsArchived = !tenant.IsArchived;
            tenant.LastModified = DateTime.UtcNow;

            await _tenantRepository.UpdateAsync(tenant, cancellationToken);

            // Invalidate cache for this tenant
            var cacheKey = $"tenant_config_{tenant.TenantId}";
            await _cacheService.RemoveAsync(cacheKey, cancellationToken);

            // Invalidate paginated tenant list cache
            await _cacheService.RemoveByPatternAsync("all_active_tenants_with_config_*", cancellationToken);

            return TenantDto.MapFrom(tenant);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
