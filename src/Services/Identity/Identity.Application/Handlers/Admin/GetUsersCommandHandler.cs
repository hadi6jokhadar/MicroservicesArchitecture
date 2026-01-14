using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Common.Mappings;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Identity.Application.DTOs;
using Identity.Application.Helpers;
using Identity.Domain.Repositories;
using Identity.Domain.Entities;
using MediatR;
using Identity.Application.Commands;

public class GetUsersCommandHandler : IRequestHandler<GetUsersCommand, PaginatedList<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ProfilePictureHelper _profilePictureHelper;
    private readonly ICurrentUserService _currentUserService;

    public GetUsersCommandHandler(
        IUserRepository userRepository,
        ProfilePictureHelper profilePictureHelper,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _profilePictureHelper = profilePictureHelper;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<UserDto>> Handle(GetUsersCommand request, CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<User> query;
            
            // If filtering by role name, get filtered users
            if (!string.IsNullOrWhiteSpace(request.RoleName))
            {
                var usersWithRole = await _userRepository.GetUsersByRoleNameAsync(request.RoleName, cancellationToken);
                query = usersWithRole.AsQueryable();
            }
            else
            {
                query = _userRepository.GetAll();
            }

            // Apply search term filter
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(u =>
                    u.FirstName.ToLower().Contains(searchTerm) ||
                    u.LastName.ToLower().Contains(searchTerm) ||
                    (u.Email != null && u.Email.ToLower().Contains(searchTerm)));
            }

            // Apply status filter
            if (request.Status.HasValue)
            {
                query = query.Where(u => u.Status == request.Status.Value);
            }

            // Order by created date (newest first)
            query = query.OrderByDescending(u => u.Created);

            // Check if requester is SuperAdmin or Admin (should include roles/claims)
            bool includeRoles = _currentUserService.IsSuperAdmin || _currentUserService.HasRole("Admin");

            // Manual projection to DTO
            var dtoQuery = query.Select(u => new UserDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = u.Status,
                Created = u.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                LastModified = u.LastModified != null ? u.LastModified.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) : null,
                Roles = includeRoles ? u.UserRoles.Select(ur => new RoleDto 
                { 
                    Id = ur.Role.Id, 
                    Name = ur.Role.Name,
                    Description = ur.Role.Description,
                    IsSystemRole = ur.Role.IsSystemRole,
                    Status = ur.Role.Status,
                    Claims = ur.Role.RoleClaims.Select(rc => new ClaimDto
                    {
                        Id = rc.Claim.Id,
                        Name = rc.Claim.Name,
                        Description = rc.Claim.Description,
                        ClaimType = rc.Claim.ClaimType,
                        ClaimValue = rc.Claim.ClaimValue,
                        IsSuperAdminOnly = rc.Claim.IsSuperAdminOnly,
                        Status = rc.Claim.Status
                    }).ToList()
                }).ToList() : new List<RoleDto>(),
                ProfilePictureId = u.ProfilePictureId,
                ProfilePicture = null, // Not populated in list view for performance
                VerificationCode = u.VerificationCode,
                Data = u.Data
            });

            var paginatedList = await dtoQuery
                .PaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);

            // Enrich all users with profile pictures in parallel
            await _profilePictureHelper.EnrichWithProfilePicturesAsync(paginatedList.Items, cancellationToken);

            return paginatedList;
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
