using Identity.Application.Services;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Common.Models;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Entities;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, UserDtoIncludesToken>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IUserService userService,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        ProfilePictureHelper profilePictureHelper,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userService = userService;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _profilePictureHelper = profilePictureHelper;
        _logger = logger;
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
                throw new ConflictException(LocalizationKeys.Exceptions.EmailAlreadyExists);
            }

            // Check if phone number exists (if provided)
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                bool phoneExists = await _userRepository.PhoneNumberExistsAsync(request.PhoneNumber, cancellationToken);
                if (phoneExists)
                {
                    throw new ConflictException(LocalizationKeys.Exceptions.PhoneAlreadyRegistered);
                }
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
                Created = DateTime.UtcNow,
                Status = true
            };

            await _userRepository.AddAsync(user, cancellationToken);

            // Assign default "User" role
            var userRole = await _roleRepository.GetByNameAsync("User", cancellationToken);
            if (userRole != null)
            {
                await _userRoleRepository.AssignRolesToUserAsync(user.Id, [userRole.Id], cancellationToken);
            }

            // Generate tokens
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during registration for email: {Email}", request.Email);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}