using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Identity.Application.Commands.Admin.Role;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Admin.Role;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, bool>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;

    public DeleteRoleCommandHandler(
        IRoleRepository roleRepository,
        ICacheService cacheService,
        ICurrentUserService currentUserService)
    {
        _roleRepository = roleRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken);
            if (role == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.RoleNotFound);

            // Only SuperAdmin can delete SuperAdmin role
            if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) && !_currentUserService.IsSuperAdmin)
                throw new ForbiddenException(LocalizationKeys.Exceptions.SuperAdminRoleProtected);

            // System roles cannot be deleted
            if (role.IsSystemRole)
                throw new BadRequestException(LocalizationKeys.Exceptions.SystemRoleCannotBeDeleted);

            await _roleRepository.DeleteAsync(role.Id, cancellationToken);

            // Invalidate caches
            await _cacheService.RemoveAsync($"roles_all", cancellationToken);
            await _cacheService.RemoveAsync($"role_{role.Id}", cancellationToken);
            await _cacheService.RemoveAsync($"role_name_{role.NormalizedName}", cancellationToken);

            return true;
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
