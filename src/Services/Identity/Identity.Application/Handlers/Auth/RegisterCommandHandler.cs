using Identity.Application.Services;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Domain.Entities;
using IhsanDev.Shared.Kernel.Enums.Identity;
using IhsanDev.Shared.Application.Exceptions;

namespace Identity.Application.Handlers;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, UserDtoIncludesToken>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IUserService userService)
    {
        _userRepository = userRepository;
        _userService = userService;
    }

    public async Task<UserDtoIncludesToken> Handle(
        RegisterCommand request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if email exists
            bool emailExists = await _userRepository.EmailExistsAsync(request.Email, cancellationToken);
            if (emailExists)
            {
                throw new ConflictException("Email is already registered");
            }

            // Create user
            var user = new User
            {
                Email = request.Email,
                PasswordHash = _userService.HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                Data = request.Data,
                Role = UserRole.User,
                Created = DateTime.UtcNow,
                Status = true
            };

            await _userRepository.AddAsync(user, cancellationToken);

            // Generate tokens
            var authResult = await _userService.GenerateTokensAsync(user);

            return authResult;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Registration failed: " + ex.Message);
        }
    }
}