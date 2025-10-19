using AutoMapper;
using IhsanDev.Shared.Application.Exceptions;
using MediatR;
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
    private readonly IMapper _mapper;

    public UpdateTenantCommandHandler(ITenantRepository tenantRepository, IMapper mapper)
    {
        _tenantRepository = tenantRepository;
        _mapper = mapper;
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

            // Update tenant properties
            tenant.TenantName = request.TenantName;
            tenant.StartDate = request.StartDate;
            tenant.ExpireDate = request.ExpireDate;
            tenant.Data = request.Data;
            tenant.IsActive = request.IsActive;
            tenant.LastModified = DateTime.UtcNow;

            await _tenantRepository.UpdateAsync(tenant, cancellationToken);

            return _mapper.Map<TenantDto>(tenant);
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
