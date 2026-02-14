using MediatR;
using Identity.Application.DTOs;

namespace Identity.Application.Commands;

public record ToggleUserArchivedStatusCommand(int UserId) : IRequest<UserDto>;
