using Identity.Application.Commands.Auth;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Infrastructure.Services.Otp;
using MediatR;

namespace Identity.Application.Handlers.Auth;

public class GetVerificationCodeByEmailCommandHandler : IRequestHandler<GetVerificationCodeByEmailCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;

    public GetVerificationCodeByEmailCommandHandler(
        IUserRepository userRepository,
        IOtpService otpService)
    {
        _userRepository = userRepository;
        _otpService = otpService;
    }

    public async Task<bool> Handle(GetVerificationCodeByEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if email exists
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (user == null)
            {
                throw new NotFoundException($"No user found with email {request.Email}");
            }

            if (!user.Status)
            {
                throw new ForbiddenException("Account is disabled");
            }

            // Generate 5-digit verification code
            var verificationCode = _otpService.GenerateCode(5);

            // Save code to user entity
            user.VerificationCode = verificationCode;
            user.LastModified = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);

            // TODO: Send code via Email
            // For now, the code is just saved to the database
            // In production, you would send it via Email using an external provider

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to generate verification code: " + ex.Message);
        }
    }
}
