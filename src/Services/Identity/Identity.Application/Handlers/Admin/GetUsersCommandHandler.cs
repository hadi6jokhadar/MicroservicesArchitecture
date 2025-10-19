using AutoMapper;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Common.Mappings;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;
using Identity.Application.Commands;
using AutoMapper.QueryableExtensions;
using IhsanDev.Shared.Application.Exceptions;

public class GetUsersCommandHandler : IRequestHandler<GetUsersCommand, PaginatedList<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public GetUsersCommandHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
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

            // Use AutoMapper's ProjectTo for efficient mapping and pagination
            var paginatedList = await Command
                .ProjectTo<UserDto>(_mapper.ConfigurationProvider)
                .PaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);

            return paginatedList;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to get users: " + ex.Message);
        }
    }
}
