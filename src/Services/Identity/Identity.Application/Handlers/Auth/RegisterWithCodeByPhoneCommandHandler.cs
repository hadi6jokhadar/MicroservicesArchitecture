using Identity.Application.Commands.Auth;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Infrastructure.Services.Otp;
using IhsanDev.Shared.Kernel.Enums.Identity;
using MediatR;

namespace Identity.Application.Handlers.Auth;

public class RegisterWithCodeByPhoneCommandHandler : IRequestHandler<RegisterWithCodeByPhoneCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;

    public RegisterWithCodeByPhoneCommandHandler(
        IUserRepository userRepository,
        IOtpService otpService)
    {
        _userRepository = userRepository;
        _otpService = otpService;
    }

    public async Task<bool> Handle(RegisterWithCodeByPhoneCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if phone number exists
            var existingUser = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber, cancellationToken);
            if (existingUser != null)
            {
                throw new ConflictException("Phone number is already registered");
            }

            // Generate verification code
            var verificationCode = _otpService.GenerateCode(5);

            // Create user without password and without email
            var user = new User
            {
                Email = null, // No email required for phone registration
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                VerificationCode = verificationCode,
                Role = UserRole.User,
                Created = DateTime.UtcNow,
                Status = true,
                PasswordHash = null // No password for code-based registration
            };

            await _userRepository.AddAsync(user, cancellationToken);

            // TODO: Send verification code via SMS/Email
            // For now, the code is just saved to the database
            // In production, you would send it via SMS using an external provider

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Registration with phone code failed: " + ex.Message);
        }
    }
}
