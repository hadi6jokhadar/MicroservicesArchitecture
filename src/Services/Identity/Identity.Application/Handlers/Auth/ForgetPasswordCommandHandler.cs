using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Services;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Commands;

public class ForgetPasswordCommandHandler : IRequestHandler<ForgetPasswordCommand, string>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;

    public ForgetPasswordCommandHandler(IUserRepository userRepository, IUserService userService)
    {
        _userRepository = userRepository;
        _userService = userService;
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
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
