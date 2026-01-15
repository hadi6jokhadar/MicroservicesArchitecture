using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Identity.Application.Commands.Admin.Role;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Admin.Role;

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;

    public UpdateRoleCommandHandler(
        IRoleRepository roleRepository,
        ICacheService cacheService,
        ICurrentUserService currentUserService)
    {
        _roleRepository = roleRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }

    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken);
            if (role == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.RoleNotFound);

            // Only SuperAdmin can update SuperAdmin role
            if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) && !_currentUserService.IsSuperAdmin)
                throw new ForbiddenException(LocalizationKeys.Exceptions.SuperAdminRoleProtected);

            // System roles cannot be renamed
            if (role.IsSystemRole && role.Name != request.Name)
                throw new BadRequestException(LocalizationKeys.Exceptions.SystemRoleCannotBeRenamed);

            role.Name = request.Name;
            role.NormalizedName = request.Name.ToUpperInvariant();
            role.Description = request.Description;
            role.LastModified = DateTime.UtcNow;

            await _roleRepository.UpdateAsync(role, cancellationToken);

            // Invalidate group caches
            await _cacheService.RemoveAsync($"admin:roles", cancellationToken);
            await _cacheService.RemoveAsync($"admin:roles:{role.Id}", cancellationToken);
            await _cacheService.RemoveAsync($"admin:roles:name_{role.NormalizedName}", cancellationToken);

            return RoleDto.MapFrom(role);
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
