using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Commands;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    public DeleteUserCommandHandler(
        IUserRepository userRepository,
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext,
        ILogger<DeleteUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdWithArchivedAsync(request.Id, cancellationToken);
            if (user == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            // If already archived, do a hard delete (permanent removal)
            // Otherwise, do a soft delete (set IsArchived = true)
            if (user.IsArchived)
            {
                // Hard delete: Remove from database permanently
                await _userRepository.HardDeleteAsync(user, cancellationToken);
            }
            else
            {
                // Soft delete: Set IsArchived = true
                await _userRepository.DeleteAsync(user, cancellationToken);
            }

            // Mark profile picture as temporary (eligible for cleanup) if exists
            if (user.ProfilePictureId.HasValue)
            {
                try
                {
                    var tenantId = _tenantContext.TenantId;
                    await _fileManagerClient.ChangeTempStatusAsync(user.ProfilePictureId.Value, true, tenantId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log warning but don't fail the operation
                    Console.WriteLine($"Warning: Failed to mark profile picture {user.ProfilePictureId} as temporary: {ex.Message}");
                }
            }

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
