using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;
using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace Identity.Application.Handlers.Commands;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;

    public DeleteUserCommandHandler(
        IUserRepository userRepository,
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
    }

    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
            if (user == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.UserNotFound);

            // Soft delete by setting IsArchived to true
            user.IsArchived = true;
            user.Status = false;
            user.LastModified = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);

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
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
