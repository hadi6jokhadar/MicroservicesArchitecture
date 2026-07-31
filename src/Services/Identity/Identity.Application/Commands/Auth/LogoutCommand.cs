using MediatR;

namespace Identity.Application.Commands.Auth;

public record LogoutCommand(int UserId) : IRequest<bool>;
