using MediatR;

namespace Identity.Application.Commands.DeviceToken;

/// <summary>
/// Delete multiple device tokens in a single batch request
/// </summary>
public record DeleteBatchDeviceTokensCommand(List<int> TokenIds) 
    : IRequest<int>; // Returns count of deleted tokens
