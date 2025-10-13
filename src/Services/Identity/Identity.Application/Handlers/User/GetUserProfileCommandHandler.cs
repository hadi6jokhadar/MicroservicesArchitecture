using AutoMapper;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;
using Identity.Application.Commands;

namespace Identity.Application.Handlers.Commands;

public class GetUserProfileCommandHandler : IRequestHandler<GetUserProfileCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public GetUserProfileCommandHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(GetUserProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found");

            var userProfile = _mapper.Map<UserDto>(user);
            return userProfile;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to get user profile: " + ex.Message);
        }
    }
}
