using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Commands;

// Toggle User Archived Status Command Handler (Admin)
public class ToggleUserArchivedStatusCommandHandler : IRequestHandler<ToggleUserArchivedStatusCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;

    public ToggleUserArchivedStatusCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
    }

    public async Task<UserDto> Handle(ToggleUserArchivedStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            user.IsArchived = !user.IsArchived;
            user.LastModified = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);

            // Admin endpoint: Always include roles
            var userDto = UserDto.MapFrom(user, includeRoles: true);
            
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
