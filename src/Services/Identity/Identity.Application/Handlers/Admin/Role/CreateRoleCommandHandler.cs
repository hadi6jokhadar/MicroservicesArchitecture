using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using Identity.Application.Commands.Admin.Role;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Admin.Role;

public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CreateRoleCommandHandler> _logger;

    public CreateRoleCommandHandler(
        IRoleRepository roleRepository,
        ICacheService cacheService,
        ILogger<CreateRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if role already exists
            var existingRole = await _roleRepository.GetByNameAsync(request.Name, cancellationToken);
            if (existingRole != null)
                throw new ConflictException(LocalizationKeys.Exceptions.RoleAlreadyExists);

            var role = new Domain.Entities.Role
            {
                Name = request.Name,
                NormalizedName = request.Name.ToUpperInvariant(),
                Description = request.Description,
                IsSystemRole = false,
                Status = true
            };

            await _roleRepository.CreateAsync(role, cancellationToken);

            // Invalidate roles group cache
            await _cacheService.RemoveAsync($"admin:roles", cancellationToken);
            await _cacheService.RemoveAsync($"admin:roles:{role.Id}", cancellationToken);

            return RoleDto.MapFrom(role);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating role {RoleName}", request.Name);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
