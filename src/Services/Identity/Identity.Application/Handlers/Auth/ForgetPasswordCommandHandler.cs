using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Services;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Exceptions;

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

            return "If the email exists, a password reset link has been sent.";
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to reset password: " + ex.Message);
        }
    }
}
