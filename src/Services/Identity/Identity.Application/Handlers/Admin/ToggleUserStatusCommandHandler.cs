using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

// Toggle User Status Command Handler (Admin)
public class ToggleUserStatusCommandHandler : IRequestHandler<ToggleUserStatusCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly ILogger<ToggleUserStatusCommandHandler> _logger;

    public ToggleUserStatusCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper,
        ILogger<ToggleUserStatusCommandHandler> logger)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
        _logger = logger;
    }

    public async Task<UserDto> Handle(ToggleUserStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            user.Status = !user.Status;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle user status");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}