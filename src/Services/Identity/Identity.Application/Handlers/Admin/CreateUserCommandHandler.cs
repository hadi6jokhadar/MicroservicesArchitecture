using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Polly.CircuitBreaker;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IUserService userService,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        ICurrentUserService currentUserService,
        ProfilePictureHelper profilePictureHelper,
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext,
        ILogger<CreateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userService = userService;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _currentUserService = currentUserService;
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

            // Only a SuperAdmin may create a user pre-assigned the SuperAdmin role — otherwise
            // a plain Admin could self-escalate by creating a fresh SuperAdmin account.
            if (request.RoleIds != null && request.RoleIds.Any() && !_currentUserService.IsSuperAdmin)
            {
                foreach (var roleId in request.RoleIds)
                {
                    var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
                    if (role != null && role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Non-SuperAdmin caller attempted to create a user with the SuperAdmin role");
                        throw new ForbiddenException(LocalizationKeys.Exceptions.SuperAdminRoleProtected);
                    }
                }
            }

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
                catch (BrokenCircuitException ex)
                {
                    _logger.LogWarning(ex, "FileManager circuit open; skipping profile picture mark for User {UserId}", user.Id);
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
