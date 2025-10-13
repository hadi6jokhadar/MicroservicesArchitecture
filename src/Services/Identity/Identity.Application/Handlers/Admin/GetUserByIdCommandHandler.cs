using AutoMapper;
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
    private readonly IMapper _mapper;

    public GetUserByIdCommandHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(GetUserByIdCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found");

            var userDto = _mapper.Map<UserDto>(user);
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