using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Repositories;
using MediatR;
using Identity.Application.Commands;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

public class GetUserProfileCommandHandler : IRequestHandler<GetUserProfileCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetUserProfileCommandHandler> _logger;

    public GetUserProfileCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper,
        ICurrentUserService currentUserService,
        ILogger<GetUserProfileCommandHandler> logger)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UserDto> Handle(GetUserProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            // Only include roles if requester is SuperAdmin or Admin
            bool includeRoles = _currentUserService.IsSuperAdmin || _currentUserService.HasRole("Admin");
            var userProfile = UserDto.MapFrom(user, includeRoles);
            
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting profile for user {UserId}", request.UserId);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
