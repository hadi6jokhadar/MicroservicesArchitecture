using Identity.Application.Commands.Admin.Role;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Admin;

public class AssignRolesToUserCommandHandler : IRequestHandler<AssignRolesToUserCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ILogger<AssignRolesToUserCommandHandler> _logger;

    public AssignRolesToUserCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        ILogger<AssignRolesToUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
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
