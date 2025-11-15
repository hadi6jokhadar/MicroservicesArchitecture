using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;
using Identity.Application.Commands;

namespace Identity.Application.Handlers.Commands;

public class GetUserByIdCommandHandler : IRequestHandler<GetUserByIdCommand, UserDto>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto> Handle(GetUserByIdCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found");

            var userDto = UserDto.MapFrom(user);
            return userDto;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to get user: " + ex.Message);
        }
    }
}