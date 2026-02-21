using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Identity.Application.Commands.Admin.Role;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Admin.Role;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, bool>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteRoleCommandHandler> _logger;

    public DeleteRoleCommandHandler(
        IRoleRepository roleRepository,
        ICacheService cacheService,
        ICurrentUserService currentUserService,
        ILogger<DeleteRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
        _logger = logger;
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

            // Invalidate group caches
            await _cacheService.RemoveAsync($"admin:roles", cancellationToken);
            await _cacheService.RemoveAsync($"admin:roles:{role.Id}", cancellationToken);
            await _cacheService.RemoveAsync($"admin:roles:name_{role.NormalizedName}", cancellationToken);

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting role {RoleId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
