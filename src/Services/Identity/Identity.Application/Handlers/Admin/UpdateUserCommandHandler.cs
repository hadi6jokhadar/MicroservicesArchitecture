using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        ProfilePictureHelper profilePictureHelper,
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext,
        ILogger<UpdateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _profilePictureHelper = profilePictureHelper;
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<UserDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
            if (user == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            // Capture old profile picture ID before update
            var oldProfilePictureId = user.ProfilePictureId;

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.PhoneNumber = request.PhoneNumber;
            user.ProfilePictureId = request.ProfilePictureId;
            user.Data = request.Data;
            user.LastModified = DateTime.UtcNow;

            if (request.EmailConfirmed.HasValue)
                user.EmailConfirmed = request.EmailConfirmed.Value;

            if (request.Status.HasValue)
                user.Status = request.Status.Value;

            await _userRepository.UpdateAsync(user, cancellationToken);

            // Update user roles
            await _userRoleRepository.RevokeAllRolesFromUserAsync(user.Id, cancellationToken);
            await _userRoleRepository.AssignRolesToUserAsync(user.Id, request.RoleIds, cancellationToken);

            // Reload user with roles to populate navigation properties
            var userWithRoles = await _userRepository.GetByIdAsync(user.Id, cancellationToken);
            if (userWithRoles == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            // Update temp status for old and new profile pictures
            var tenantId = _tenantContext.TenantId;

            // Remove usage row for old picture if it changed (may set Temp=true if no other usages)
            if (oldProfilePictureId.HasValue && oldProfilePictureId != request.ProfilePictureId)
            {
                try
                {
                    await _fileManagerClient.ChangeTempStatusAsync(oldProfilePictureId.Value, "User", user.Id.ToString(), false, tenantId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log warning but don't fail the operation
                    Console.WriteLine($"Warning: Failed to remove usage for old profile picture {oldProfilePictureId}: {ex.Message}");
                }
            }

            // Add usage row for new picture (sets Temp=false)
            if (request.ProfilePictureId.HasValue)
            {
                try
                {
                    await _fileManagerClient.ChangeTempStatusAsync(request.ProfilePictureId.Value, "User", user.Id.ToString(), true, tenantId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log warning but don't fail the operation
                    Console.WriteLine($"Warning: Failed to add usage for new profile picture {request.ProfilePictureId}: {ex.Message}");
                }
            }

            // Admin endpoint: Always include roles
            var userDto = UserDto.MapFrom(userWithRoles, includeRoles: true);
            
            // Enrich with profile picture
            await _profilePictureHelper.EnrichWithProfilePictureAsync(
                userDto,
                user.ProfilePictureId,
                user.Id,
                cancellationToken);
            
            return userDto;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
