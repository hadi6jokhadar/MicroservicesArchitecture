using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using MediatR;
using System.Text.Json;
using Tenant.Application.Commands.Tenant;
using Tenant.Application.DTOs;
using Tenant.Domain.Entities;
using Tenant.Domain.Repositories;

namespace Tenant.Application.Handlers.Tenant;

/// <summary>
/// Handler for creating new tenant
/// </summary>
public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICacheService _cacheService;

    public CreateTenantCommandHandler(ITenantRepository tenantRepository, ICacheService cacheService)
    {
        _tenantRepository = tenantRepository;
        _cacheService = cacheService;
    }

    public async Task<TenantDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if tenant ID already exists
            var tenantExists = await _tenantRepository.TenantIdExistsAsync(request.TenantId, cancellationToken);
            if (tenantExists)
            {
                throw new ConflictException($"Tenant with ID '{request.TenantId}' already exists");
            }

            // Check if user already has a tenant
            // var userHasTenant = await _tenantRepository.UserHasTenantAsync(request.UserId, cancellationToken);
            // if (userHasTenant)
            // {
            //     throw new ConflictException($"User with ID '{request.UserId}' already has a tenant");
            // }

            // Serialize TenantConfiguration to JSON string for database storage
            var dataJson = JsonSerializer.Serialize(request.Data, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Create tenant
            var tenantSettings = new TenantSettings
            {
                TenantId = request.TenantId,
                TenantName = request.TenantName,
                UserId = request.UserId,
                StartDate = request.StartDate,
                ExpireDate = request.ExpireDate,
                Data = dataJson,
                IsActive = true,
                Created = DateTime.UtcNow
            };

            var created = await _tenantRepository.AddAsync(tenantSettings, cancellationToken);

            // Cache the new tenant immediately for instant availability
            var tenantInfo = new TenantInfo
            {
                TenantId = created.TenantId,
                TenantName = created.TenantName,
                UserId = created.UserId,
                IsActive = created.IsActive,
                Configuration = request.Data
            };

            var cacheKey = $"tenant_config_{created.TenantId}";
            var cacheExpiration = TimeSpan.FromDays(7); // 7 days cache, invalidated on updates
            await _cacheService.SetAsync(cacheKey, tenantInfo, cacheExpiration, cancellationToken);

            // Invalidate paginated tenant list cache (new tenant added)
            await _cacheService.RemoveByPatternAsync("all_active_tenants_with_config_*", cancellationToken);

            return TenantDto.MapFrom(created);
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
