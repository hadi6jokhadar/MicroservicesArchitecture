using AutoMapper;
using IhsanDev.Shared.Application.Exceptions;
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
    private readonly IMapper _mapper;

    public CreateTenantCommandHandler(ITenantRepository tenantRepository, IMapper mapper)
    {
        _tenantRepository = tenantRepository;
        _mapper = mapper;
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

            return _mapper.Map<TenantDto>(created);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to create tenant: " + ex.Message);
        }
    }
}
