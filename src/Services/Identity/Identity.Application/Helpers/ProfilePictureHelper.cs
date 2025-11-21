using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Identity.Application.DTOs;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Helpers;

/// <summary>
/// Helper class for fetching profile pictures from FileManager service
/// </summary>
public class ProfilePictureHelper
{
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ProfilePictureHelper> _logger;

    public ProfilePictureHelper(
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext,
        ILogger<ProfilePictureHelper> logger)
    {
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Enriches a UserDto with profile picture data from FileManager service
    /// </summary>
    /// <param name="userDto">The user DTO to enrich</param>
    /// <param name="profilePictureId">The profile picture ID to fetch</param>
    /// <param name="userId">The user ID for logging purposes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The enriched UserDto (same instance, modified)</returns>
    public async Task<UserDto> EnrichWithProfilePictureAsync(
        UserDto userDto,
        int? profilePictureId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!profilePictureId.HasValue)
        {
            return userDto;
        }

        try
        {
            var tenantId = _tenantContext.CurrentTenant?.TenantId;

            userDto.ProfilePicture = await _fileManagerClient.GetFileByIdAsync(
                profilePictureId.Value,
                tenantId,
                cancellationToken);

            if (userDto.ProfilePicture == null)
            {
                _logger.LogWarning(
                    "Profile picture {ProfilePictureId} not found for user {UserId}",
                    profilePictureId.Value,
                    userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to fetch profile picture {ProfilePictureId} for user {UserId}",
                profilePictureId.Value,
                userId);
            // Continue without profile picture (graceful degradation)
        }

        return userDto;
    }

    /// <summary>
    /// Enriches multiple UserDtos with profile pictures in parallel
    /// </summary>
    /// <param name="userDtos">The list of user DTOs to enrich</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The enriched list (same instances, modified)</returns>
    public async Task<IEnumerable<UserDto>> EnrichWithProfilePicturesAsync(
        IEnumerable<UserDto> userDtos,
        CancellationToken cancellationToken = default)
    {
        var userList = userDtos.ToList();
        
        // Get all unique profile picture IDs that need fetching
        var pictureIds = userList
            .Where(u => u.ProfilePictureId.HasValue)
            .Select(u => u.ProfilePictureId!.Value)
            .Distinct()
            .ToList();

        if (!pictureIds.Any())
        {
            return userList;
        }

        try
        {
            var tenantId = _tenantContext.CurrentTenant?.TenantId;

            // Fetch all profile pictures in a single batch request
            var picturesDict = await _fileManagerClient.GetFilesByIdsAsync(
                pictureIds, 
                tenantId, 
                cancellationToken);

            // Enrich each user with their profile picture
            foreach (var user in userList.Where(u => u.ProfilePictureId.HasValue))
            {
                if (picturesDict.TryGetValue(user.ProfilePictureId!.Value, out var picture))
                {
                    user.ProfilePicture = picture;
                }
                else
                {
                    _logger.LogWarning(
                        "Profile picture {ProfilePictureId} not found for user {UserId}",
                        user.ProfilePictureId.Value,
                        user.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to batch fetch profile pictures for {Count} users",
                userList.Count);
            // Continue without profile pictures (graceful degradation)
        }

        return userList;
    }

    /// <summary>
    /// Enriches a UserDtoIncludesToken with profile picture data from FileManager service
    /// </summary>
    /// <param name="userDto">The user DTO to enrich</param>
    /// <param name="profilePictureId">The profile picture ID to fetch</param>
    /// <param name="userId">The user ID for logging purposes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The enriched UserDtoIncludesToken (same instance, modified)</returns>
    public async Task<UserDtoIncludesToken> EnrichWithProfilePictureAsync(
        UserDtoIncludesToken userDto,
        int? profilePictureId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!profilePictureId.HasValue)
        {
            return userDto;
        }

        try
        {
            var tenantId = _tenantContext.CurrentTenant?.TenantId;

            userDto.ProfilePicture = await _fileManagerClient.GetFileByIdAsync(
                profilePictureId.Value,
                tenantId,
                cancellationToken);

            if (userDto.ProfilePicture == null)
            {
                _logger.LogWarning(
                    "Profile picture {ProfilePictureId} not found for user {UserId}",
                    profilePictureId.Value,
                    userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to fetch profile picture {ProfilePictureId} for user {UserId}",
                profilePictureId.Value,
                userId);
            // Continue without profile picture (graceful degradation)
        }

        return userDto;
    }
}
