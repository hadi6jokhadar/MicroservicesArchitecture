using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Commands;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
    }

    public async Task<UserDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
            if (user == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.Role = request.Role;
            user.PhoneNumber = request.PhoneNumber;
            user.Data = request.Data;
            user.LastModified = DateTime.UtcNow;

            if (request.EmailConfirmed.HasValue)
                user.EmailConfirmed = request.EmailConfirmed.Value;

            if (request.Status.HasValue)
                user.Status = request.Status.Value;

            await _userRepository.UpdateAsync(user, cancellationToken);

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
