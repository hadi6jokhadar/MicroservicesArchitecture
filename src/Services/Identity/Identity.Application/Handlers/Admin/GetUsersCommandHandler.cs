using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Common.Mappings;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;
using Identity.Application.Commands;

public class GetUsersCommandHandler : IRequestHandler<GetUsersCommand, PaginatedList<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUsersCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PaginatedList<UserDto>> Handle(GetUsersCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var Command = _userRepository.GetAll();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                Command = Command.Where(u =>
                    u.FirstName.ToLower().Contains(searchTerm) ||
                    u.LastName.ToLower().Contains(searchTerm) ||
                    (u.Email != null && u.Email.ToLower().Contains(searchTerm)));
            }

            if (request.Role.HasValue)
            {
                Command = Command.Where(u => u.Role == request.Role);
            }

            if (request.Status.HasValue)
            {
                Command = Command.Where(u => u.Status == request.Status.Value);
            }

            // Order by created date (newest first)
            Command = Command.OrderByDescending(u => u.Created);

            // Manual projection to DTO
            var dtoQuery = Command.Select(u => new UserDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = u.Status,
                Created = u.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                LastModified = u.LastModified != null ? u.LastModified.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) : null,
                Role = u.Role,
                RoleName = u.Role.ToString(),
                ProfilePictureUrl = u.ProfilePictureUrl,
                VerificationCode = u.VerificationCode,
                Data = u.Data
            });

            var paginatedList = await dtoQuery
                .PaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);

            return paginatedList;
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
