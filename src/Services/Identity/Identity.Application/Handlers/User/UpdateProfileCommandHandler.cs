using AutoMapper;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Commands;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public UpdateProfileCommandHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Id == null || request.Id <= 0)
                throw new BadRequestException("Invalid user ID");
                
            var user = await _userRepository.GetByIdAsync((int)request.Id, cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found");

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.PhoneNumber = request.PhoneNumber;
            user.ProfilePictureUrl = request.ProfilePictureUrl;
            user.LastModified = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);

            var userProfile = _mapper.Map<UserDto>(user);
            return userProfile;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeneralException("Profile update failed: " + ex.Message);
        }
    }
}
