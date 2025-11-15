using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Commands;

// Toggle User Status Command Handler (Admin)
public class ToggleUserStatusCommandHandler : IRequestHandler<ToggleUserStatusCommand, UserDto>
{
    private readonly IUserRepository _userRepository;

    public ToggleUserStatusCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto> Handle(ToggleUserStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found");

            user.Status = !user.Status;
            user.LastModified = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);

            var userDto = UserDto.MapFrom(user);
            return userDto;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Failed to toggle user status: " + ex.Message);
        }
    }
}