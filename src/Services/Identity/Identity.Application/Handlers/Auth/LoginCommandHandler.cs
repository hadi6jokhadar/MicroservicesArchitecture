using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Application.Services;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Commands;

public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDtoIncludesToken>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly ProfilePictureHelper _profilePictureHelper;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IUserService userService,
        ProfilePictureHelper profilePictureHelper)
    {
        _userRepository = userRepository;
        _userService = userService;
        _profilePictureHelper = profilePictureHelper;
    }

    public async Task<UserDtoIncludesToken> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (user == null)
                throw new UnauthorizedException(LocalizationKeys.Exceptions.InvalidCredentials);

            if (!user.Status)
                throw new ForbiddenException(LocalizationKeys.Exceptions.AccountDisabled);

            if (string.IsNullOrEmpty(user.PasswordHash) || !_userService.VerifyPassword(request.Password, user.PasswordHash))
                throw new UnauthorizedException(LocalizationKeys.Exceptions.InvalidCredentials);

            user.LastLogin = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);

            var authResult = await _userService.GenerateTokensAsync(user);
            
            // Enrich with profile picture
            await _profilePictureHelper.EnrichWithProfilePictureAsync(
                authResult,
                user.ProfilePictureId,
                user.Id,
                cancellationToken);
            
            return authResult;
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
