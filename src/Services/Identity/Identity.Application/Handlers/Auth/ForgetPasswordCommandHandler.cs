using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Services;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

public class ForgetPasswordCommandHandler : IRequestHandler<ForgetPasswordCommand, string>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly ILogger<ForgetPasswordCommandHandler> _logger;

    public ForgetPasswordCommandHandler(IUserRepository userRepository, IUserService userService, ILogger<ForgetPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userService = userService;
        _logger = logger;
    }

    public async Task<string> Handle(ForgetPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

            // Always return success to prevent email enumeration
            if (user != null && user.Status)
            {
                var resetToken = _userService.GeneratePasswordResetToken();
                // TODO: Send email with reset token
                // In a real application, you would implement email service here
            }

            return LocalizationKeys.Success.PasswordResetEmailSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing forget password for email: {Email}", request.Email);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
