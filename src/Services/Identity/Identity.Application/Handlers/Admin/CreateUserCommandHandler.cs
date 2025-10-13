using AutoMapper;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Commands;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public CreateUserCommandHandler(IUserRepository userRepository, IUserService userService, IMapper mapper)
    {
        _userRepository = userRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (existingUser != null)
                throw new ConflictException("User with this email already exists");

            var hashedPassword = _userService.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email,
                PasswordHash = hashedPassword,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = request.Role,
                PhoneNumber = request.PhoneNumber,
                Created = DateTime.UtcNow,
                Status = true,
                EmailConfirmed = false
            };

            await _userRepository.AddAsync(user, cancellationToken);

            var userDto = _mapper.Map<UserDto>(user);
            return userDto;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to create user: " + ex.Message);
        }
    }
}
