using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Services;
using MediatR;

namespace Identity.Application.Handlers;

// Refresh Token Command Handler
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, UserDtoIncludesToken>
{
    private readonly IUserService _userService;

    public RefreshTokenCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<UserDtoIncludesToken> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var authResult = await _userService.RefreshTokenAsync(request.RefreshToken);
            
            if (authResult == null)
                throw new UnauthorizedException(LocalizationKeys.Exceptions.InvalidToken);

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