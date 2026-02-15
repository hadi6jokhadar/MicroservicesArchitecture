using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Repositories;
using MediatR;
using Identity.Application.Commands;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

public class GetUserByIdCommandHandler : IRequestHandler<GetUserByIdCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly ILogger<GetUserByIdCommandHandler> _logger;

    public GetUserByIdCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper,
        ILogger<GetUserByIdCommandHandler> logger)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
        _logger = logger;
    }

    public async Task<UserDto> Handle(GetUserByIdCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            // Admin endpoint: Always include roles
            var userDto = UserDto.MapFrom(user, includeRoles: true);
            
            // Always enrich with profile picture if available
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
            _logger.LogError(ex, "Failed to get user by id");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}