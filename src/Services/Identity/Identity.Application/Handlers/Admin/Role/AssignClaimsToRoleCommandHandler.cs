using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Identity.Application.Commands.Admin.Role;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Admin.Role;

public class AssignClaimsToRoleCommandHandler : IRequestHandler<AssignClaimsToRoleCommand, bool>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRoleClaimRepository _roleClaimRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AssignClaimsToRoleCommandHandler> _logger;

    public AssignClaimsToRoleCommandHandler(
        IRoleRepository roleRepository,
        IRoleClaimRepository roleClaimRepository,
        ICacheService cacheService,
        ICurrentUserService currentUserService,
        ILogger<AssignClaimsToRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _roleClaimRepository = roleClaimRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<bool> Handle(AssignClaimsToRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
            if (role == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.RoleNotFound);

            // Only SuperAdmin can assign claims to SuperAdmin role
            if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) && !_currentUserService.IsSuperAdmin)
                throw new ForbiddenException(LocalizationKeys.Exceptions.SuperAdminRoleProtected);

            // Revoke existing claims and assign new ones
            await _roleClaimRepository.RevokeAllClaimsFromRoleAsync(request.RoleId, cancellationToken);
            await _roleClaimRepository.AssignClaimsToRoleAsync(request.RoleId, request.ClaimIds, cancellationToken);

            // Invalidate group caches
            await _cacheService.RemoveAsync($"admin:roles", cancellationToken);
            await _cacheService.RemoveAsync($"admin:roles:{request.RoleId}", cancellationToken);
            await _cacheService.RemoveAsync($"admin:roles:name_{role.NormalizedName}", cancellationToken);
            await _cacheService.RemoveAsync($"admin:roles:{request.RoleId}:claims", cancellationToken);

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while assigning claims to role {RoleId}", request.RoleId);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
