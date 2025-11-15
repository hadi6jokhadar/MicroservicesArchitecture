using IhsanDev.Shared.Application.Exceptions;
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

    public UpdateTenantCommandHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<TenantDto> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
            if (tenant == null)
            {
                throw new NotFoundException($"Tenant with ID '{request.TenantId}' not found");
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

            return TenantDto.MapFrom(tenant);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to update tenant: " + ex.Message);
        }
    }
}
