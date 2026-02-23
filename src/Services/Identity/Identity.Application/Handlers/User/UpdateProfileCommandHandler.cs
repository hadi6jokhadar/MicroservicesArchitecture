using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly ITenantContext _tenantContext;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateProfileCommandHandler> _logger;

    public UpdateProfileCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper,
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        ILogger<UpdateProfileCommandHandler> logger)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UserDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Id == null || request.Id <= 0)
                throw new BadRequestException(LocalizationKeys.Exceptions.InvalidUserId);
                
            var user = await _userRepository.GetByIdAsync((int)request.Id, cancellationToken);
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

            await _userRepository.UpdateAsync(user, cancellationToken);

            // Update temp status for old and new profile pictures
            var tenantId = _tenantContext.TenantId;

            // Mark old file as temporary (eligible for cleanup) if it changed
            if (oldProfilePictureId.HasValue && oldProfilePictureId != request.ProfilePictureId)
            {
                try
                {
                    await _fileManagerClient.ChangeTempStatusAsync(oldProfilePictureId.Value, true, tenantId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log warning but don't fail the operation
                    _logger.LogWarning(ex, "Warning: Failed to mark old profile picture {OldProfilePictureId} as temporary", oldProfilePictureId);
                }
            }

            // Mark new file as permanent if provided
            if (request.ProfilePictureId.HasValue)
            {
                try
                {
                    await _fileManagerClient.ChangeTempStatusAsync(request.ProfilePictureId.Value, false, tenantId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log warning but don't fail the operation
                    _logger.LogWarning(ex, "Warning: Failed to mark new profile picture {NewProfilePictureId} as permanent", request.ProfilePictureId);
                }
            }

            // Include roles for the user profile response
            bool includeRoles = true;
            var userProfile = UserDto.MapFrom(user, includeRoles);
            
            // Enrich with profile picture
            await _profilePictureHelper.EnrichWithProfilePictureAsync(
                userProfile,
                user.ProfilePictureId,
                user.Id,
                cancellationToken);
            
            return userProfile;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating profile for user {UserId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
