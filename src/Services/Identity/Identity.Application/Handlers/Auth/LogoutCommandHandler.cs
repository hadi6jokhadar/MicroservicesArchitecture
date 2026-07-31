using Identity.Application.Commands.Auth;
using Identity.Application.Services;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Auth;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly IUserService _userService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(IUserService userService, ILogger<LogoutCommandHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _userService.RevokeTokenAsync(request.UserId);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during logout for user {UserId}", request.UserId);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
