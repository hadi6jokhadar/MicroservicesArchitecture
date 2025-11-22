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

namespace Identity.Application.Handlers.Commands;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper,
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
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
            user.Role = request.Role;
            user.PhoneNumber = request.PhoneNumber;
            user.ProfilePictureId = request.ProfilePictureId;
            user.Data = request.Data;
            user.LastModified = DateTime.UtcNow;

            if (request.EmailConfirmed.HasValue)
                user.EmailConfirmed = request.EmailConfirmed.Value;

            if (request.Status.HasValue)
                user.Status = request.Status.Value;

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
                    Console.WriteLine($"Warning: Failed to mark old profile picture {oldProfilePictureId} as temporary: {ex.Message}");
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
                    Console.WriteLine($"Warning: Failed to mark new profile picture {request.ProfilePictureId} as permanent: {ex.Message}");
                }
            }

            var userDto = UserDto.MapFrom(user);
            
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
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
