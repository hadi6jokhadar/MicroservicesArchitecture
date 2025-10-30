using Identity.Application.Commands.Auth;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Infrastructure.Services.Otp;
using IhsanDev.Shared.Kernel.Enums.Identity;
using MediatR;

namespace Identity.Application.Handlers.Auth;

public class RegisterWithCodeByEmailCommandHandler : IRequestHandler<RegisterWithCodeByEmailCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;

    public RegisterWithCodeByEmailCommandHandler(
        IUserRepository userRepository,
        IOtpService otpService)
    {
        _userRepository = userRepository;
        _otpService = otpService;
    }

    public async Task<bool> Handle(RegisterWithCodeByEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if email exists
            bool emailExists = await _userRepository.EmailExistsAsync(request.Email, cancellationToken);
            if (emailExists)
            {
                throw new ConflictException("Email is already registered");
            }

            // Generate verification code
            var verificationCode = _otpService.GenerateCode(5);

            // Create user without password and without phone
            var user = new User
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = null, // No phone required for email registration
                VerificationCode = verificationCode,
                Role = UserRole.User,
                Created = DateTime.UtcNow,
                Status = true,
                PasswordHash = null // No password for code-based registration
            };

            await _userRepository.AddAsync(user, cancellationToken);

            // TODO: Send verification code via Email
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
            throw new GeneralException("Registration with email code failed: " + ex.Message);
        }
    }
}
