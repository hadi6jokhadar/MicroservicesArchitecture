using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Command to delete a device token by ID
/// </summary>
public record DeleteDeviceTokenCommand(int Id) : IRequest<bool>;
