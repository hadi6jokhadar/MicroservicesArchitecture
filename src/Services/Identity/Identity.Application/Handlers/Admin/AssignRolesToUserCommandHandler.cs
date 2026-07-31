using Identity.Application.Commands.Admin.Role;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Admin;

public class AssignRolesToUserCommandHandler : IRequestHandler<AssignRolesToUserCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AssignRolesToUserCommandHandler> _logger;

    public AssignRolesToUserCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        ICurrentUserService currentUserService,
        ILogger<AssignRolesToUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<bool> Handle(AssignRolesToUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", request.UserId);
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);
            }

            if (request.RoleIds.Any())
            {
                foreach (var roleId in request.RoleIds)
                {
                    var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
                    if (role == null)
                    {
                        _logger.LogWarning("Role with ID {RoleId} not found", roleId);
                        throw new NotFoundException(LocalizationKeys.Exceptions.RoleNotFound);
                    }

                    // Only a SuperAdmin may grant the SuperAdmin role — otherwise a plain Admin
                    // could self-escalate via this endpoint (no other check here inspected
                    // RoleIds at all before this fix).
                    if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) && !_currentUserService.IsSuperAdmin)
                    {
                        _logger.LogWarning(
                            "Non-SuperAdmin caller attempted to assign the SuperAdmin role to user {UserId}",
                            request.UserId);
                        throw new ForbiddenException(LocalizationKeys.Exceptions.SuperAdminRoleProtected);
                    }
                }
            }

            await _userRoleRepository.RevokeAllRolesFromUserAsync(request.UserId, cancellationToken);
            _logger.LogInformation("Revoked all existing roles from user {UserId}", request.UserId);

            if (request.RoleIds.Any())
            {
                await _userRoleRepository.AssignRolesToUserAsync(request.UserId, request.RoleIds, cancellationToken);
                _logger.LogInformation("Assigned {RoleCount} roles to user {UserId}", request.RoleIds.Count, request.UserId);
            }
            else
            {
                _logger.LogInformation("User {UserId} now has no roles assigned", request.UserId);
            }

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign roles to user");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
