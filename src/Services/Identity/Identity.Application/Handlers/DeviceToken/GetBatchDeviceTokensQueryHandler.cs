using Identity.Application.Commands.DeviceToken;
using Identity.Domain.Repositories;
using IhsanDev.Shared.Kernel.Dto;
using MediatR;

namespace Identity.Application.Handlers.DeviceToken;

/// <summary>
/// Handler for getting device tokens for multiple users in a single batch
/// </summary>
public class GetBatchDeviceTokensQueryHandler : IRequestHandler<GetBatchDeviceTokensQuery, Dictionary<int, List<DeviceTokenDto>>>
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;

    public GetBatchDeviceTokensQueryHandler(IDeviceTokenRepository deviceTokenRepository)
    {
        _deviceTokenRepository = deviceTokenRepository;
    }

    public async Task<Dictionary<int, List<DeviceTokenDto>>> Handle(GetBatchDeviceTokensQuery request, CancellationToken cancellationToken)
    {
        if (!request.UserIds.Any())
        {
            return new Dictionary<int, List<DeviceTokenDto>>();
        }

        var result = new Dictionary<int, List<DeviceTokenDto>>();

        // Fetch all device tokens for all users in parallel
        var tasks = request.UserIds.Select(async userId =>
        {
            var tokens = await _deviceTokenRepository.GetByUserIdAsync(userId, cancellationToken);
            return new { UserId = userId, Tokens = tokens };
        });

        var allTokens = await Task.WhenAll(tasks);

        // Group by userId
        foreach (var userTokens in allTokens)
        {
            result[userTokens.UserId] = userTokens.Tokens.Select(MapToDto).ToList();
        }

        return result;
    }

    private static DeviceTokenDto MapToDto(IhsanDev.Shared.Kernel.Entities.DeviceToken deviceToken)
    {
        return new DeviceTokenDto
        {
            Id = deviceToken.Id,
            UserId = deviceToken.UserId,
            Token = deviceToken.Token,
            Platform = deviceToken.Platform,
            DeviceIdentifier = deviceToken.DeviceIdentifier,
            LastVerifiedAt = deviceToken.LastVerifiedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            IsPrimary = deviceToken.IsPrimary,
            Created = deviceToken.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
