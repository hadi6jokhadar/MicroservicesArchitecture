using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IUserService userService,
        IUserRoleRepository userRoleRepository,
        ProfilePictureHelper profilePictureHelper,
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext,
        ILogger<CreateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userService = userService;
        _userRoleRepository = userRoleRepository;
        _profilePictureHelper = profilePictureHelper;
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (existingUser != null)
                throw new ConflictException(LocalizationKeys.Exceptions.EmailAlreadyExists);

            var hashedPassword = _userService.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email,
                PasswordHash = hashedPassword,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                ProfilePictureId = request.ProfilePictureId,
                Data = request.Data,
                Created = DateTime.UtcNow,
                Status = true,
                EmailConfirmed = false
            };

            await _userRepository.AddAsync(user, cancellationToken);

            // Assign roles to user
            if (request.RoleIds != null && request.RoleIds.Any())
            {
                await _userRoleRepository.AssignRolesToUserAsync(user.Id, request.RoleIds, cancellationToken);
            }

            // Reload user with roles to populate navigation properties
            var userWithRoles = await _userRepository.GetByIdAsync(user.Id, cancellationToken);
            if (userWithRoles == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            // Mark profile picture as in-use (permanent) if provided
            if (request.ProfilePictureId.HasValue)
            {
                try
                {
                    var tenantId = _tenantContext.TenantId;
                    await _fileManagerClient.ChangeTempStatusAsync(request.ProfilePictureId.Value, "User", user.Id.ToString(), true, tenantId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log warning but don't fail the operation
                    Console.WriteLine($"Warning: Failed to mark profile picture {request.ProfilePictureId} as permanent: {ex.Message}");
                }
            }

            // Admin endpoint: Always include roles
            var userDto = UserDto.MapFrom(userWithRoles, includeRoles: true);
            
            // Enrich with profile picture (will be null for new users unless profilePictureId was provided)
            await _profilePictureHelper.EnrichWithProfilePictureAsync(
                userDto,
                user.ProfilePictureId,
                user.Id,
                cancellationToken);
            
            return userDto;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
