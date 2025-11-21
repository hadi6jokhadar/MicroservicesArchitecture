using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Repositories;
using MediatR;
using Identity.Application.Commands;

namespace Identity.Application.Handlers.Commands;

public class GetUserProfileCommandHandler : IRequestHandler<GetUserProfileCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;

    public GetUserProfileCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
    }

    public async Task<UserDto> Handle(GetUserProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            var userProfile = UserDto.MapFrom(user);
            
            // Always enrich with profile picture if available
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
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
