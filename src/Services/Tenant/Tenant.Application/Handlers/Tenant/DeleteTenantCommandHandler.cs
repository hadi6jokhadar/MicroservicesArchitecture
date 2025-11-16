using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using MediatR;
using Tenant.Application.Commands.Tenant;
using Tenant.Domain.Repositories;

namespace Tenant.Application.Handlers.Tenant;

/// <summary>
/// Handler for deleting tenant
/// </summary>
public class DeleteTenantCommandHandler : IRequestHandler<DeleteTenantCommand, bool>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICacheService _cacheService;

    public DeleteTenantCommandHandler(ITenantRepository tenantRepository, ICacheService cacheService)
    {
        _tenantRepository = tenantRepository;
        _cacheService = cacheService;
    }

    public async Task<bool> Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
            if (tenant == null)
            {
                throw new NotFoundException($"Tenant with ID '{request.TenantId}' not found");
            }

            await _tenantRepository.DeleteAsync(tenant.Id, cancellationToken);

            // Invalidate cache for this tenant
            var cacheKey = $"tenant_config_{tenant.TenantId}";
            await _cacheService.RemoveAsync(cacheKey, cancellationToken);

            // Invalidate paginated tenant list cache (tenant removed)
            await _cacheService.RemoveByPatternAsync("all_active_tenants_with_config_*", cancellationToken);

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to delete tenant: " + ex.Message);
        }
    }
}
