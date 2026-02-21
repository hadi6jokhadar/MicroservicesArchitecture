using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Identity.Application.Commands.Admin.Claim;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Admin.Claim;

public class DeleteClaimCommandHandler : IRequestHandler<DeleteClaimCommand, bool>
{
    private readonly IClaimRepository _claimRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteClaimCommandHandler> _logger;

    public DeleteClaimCommandHandler(
        IClaimRepository claimRepository,
        ICacheService cacheService,
        ICurrentUserService currentUserService,
        ILogger<DeleteClaimCommandHandler> logger)
    {
        _claimRepository = claimRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteClaimCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var claim = await _claimRepository.GetByIdAsync(request.Id, cancellationToken);
            if (claim == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.ClaimNotFound);

            // Only SuperAdmin can delete SuperAdmin-only claims
            if (claim.IsSuperAdminOnly && !_currentUserService.IsSuperAdmin)
                throw new ForbiddenException(LocalizationKeys.Exceptions.SuperAdminClaimProtected);

            await _claimRepository.DeleteAsync(claim.Id, cancellationToken);

            // Invalidate group caches
            await _cacheService.RemoveAsync($"admin:claims", cancellationToken);
            await _cacheService.RemoveAsync($"admin:claims:{claim.Id}", cancellationToken);
            await _cacheService.RemoveAsync($"admin:claims:name_{claim.Name.ToUpperInvariant()}", cancellationToken);

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting claim {ClaimId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
