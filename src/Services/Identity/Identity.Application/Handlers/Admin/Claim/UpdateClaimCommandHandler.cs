using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Identity.Application.Commands.Admin.Claim;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Admin.Claim;

public class UpdateClaimCommandHandler : IRequestHandler<UpdateClaimCommand, ClaimDto>
{
    private readonly IClaimRepository _claimRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;

    public UpdateClaimCommandHandler(
        IClaimRepository claimRepository,
        ICacheService cacheService,
        ICurrentUserService currentUserService)
    {
        _claimRepository = claimRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }

    public async Task<ClaimDto> Handle(UpdateClaimCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var claim = await _claimRepository.GetByIdAsync(request.Id, cancellationToken);
            if (claim == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.ClaimNotFound);

            // Only SuperAdmin can update SuperAdmin-only claims
            if (claim.IsSuperAdminOnly && !_currentUserService.IsSuperAdmin)
                throw new ForbiddenException(LocalizationKeys.Exceptions.SuperAdminClaimProtected);

            claim.Name = request.Name;
            claim.NormalizedName = request.Name.ToUpperInvariant();
            claim.ClaimType = request.ClaimType;
            claim.ClaimValue = request.ClaimValue;
            claim.IsSuperAdminOnly = request.IsSuperAdminOnly;
            claim.Description = request.Description;
            claim.LastModified = DateTime.UtcNow;

            await _claimRepository.UpdateAsync(claim, cancellationToken);

            // Invalidate caches
            await _cacheService.RemoveAsync($"claims_all", cancellationToken);
            await _cacheService.RemoveAsync($"claim_{claim.Id}", cancellationToken);
            await _cacheService.RemoveAsync($"claim_name_{claim.Name.ToUpperInvariant()}", cancellationToken);

            return ClaimDto.MapFrom(claim);
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
