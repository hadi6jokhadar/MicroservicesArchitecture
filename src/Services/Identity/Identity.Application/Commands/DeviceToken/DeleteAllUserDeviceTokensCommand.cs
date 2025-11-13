using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Command to delete all device tokens for a user
/// </summary>
public record DeleteAllUserDeviceTokensCommand(int UserId) : IRequest<bool>;
