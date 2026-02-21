using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using Identity.Application.Commands.Admin.Claim;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Admin.Claim;

public class CreateClaimCommandHandler : IRequestHandler<CreateClaimCommand, ClaimDto>
{
    private readonly IClaimRepository _claimRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CreateClaimCommandHandler> _logger;

    public CreateClaimCommandHandler(
        IClaimRepository claimRepository,
        ICacheService cacheService,
        ILogger<CreateClaimCommandHandler> logger)
    {
        _claimRepository = claimRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<ClaimDto> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if claim already exists
            var existingClaim = await _claimRepository.GetByClaimValueAsync(request.ClaimValue, cancellationToken);
            if (existingClaim != null)
                throw new ConflictException(LocalizationKeys.Exceptions.ClaimAlreadyExists);

            var claim = new Domain.Entities.Claim
            {
                Name = request.Name,
                NormalizedName = request.Name.ToUpperInvariant(),
                ClaimType = request.ClaimType,
                ClaimValue = request.ClaimValue,
                IsSuperAdminOnly = request.IsSuperAdminOnly,
                Description = request.Description,
                Status = true
            };

            await _claimRepository.CreateAsync(claim, cancellationToken);

            // Invalidate claims group cache
            await _cacheService.RemoveAsync($"admin:claims", cancellationToken);
            await _cacheService.RemoveAsync($"admin:claims:{claim.Id}", cancellationToken);

            return ClaimDto.MapFrom(claim);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating claim {ClaimName}", request.Name);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
