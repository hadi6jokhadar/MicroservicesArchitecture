using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using Identity.Application.Queries.Claim;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Handlers.Admin.Claim;

public class GetClaimsQueryHandler : IRequestHandler<GetClaimsQuery, List<ClaimDto>>
{
    private readonly IClaimRepository _claimRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetClaimsQueryHandler> _logger;

    public GetClaimsQueryHandler(
        IClaimRepository claimRepository,
        ICacheService cacheService,
        ILogger<GetClaimsQueryHandler> logger)
    {
        _claimRepository = claimRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<List<ClaimDto>> Handle(GetClaimsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Try group cache first
            const string groupKey = "admin:claims";
            var cachedClaims = await _cacheService.GetAsync<List<ClaimDto>>(groupKey, cancellationToken);
            if (cachedClaims != null)
                return cachedClaims;

            // Cache miss - fetch from database
            var claims = await _claimRepository.GetAllAsync(false, cancellationToken);
            var claimDtos = claims.Select(ClaimDto.MapFrom).ToList();

            // Cache under group for 30 minutes
            await _cacheService.SetAsync(groupKey, claimDtos, TimeSpan.FromMinutes(30), cancellationToken);

            return claimDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting claims");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}

public class GetClaimByIdQueryHandler : IRequestHandler<GetClaimByIdQuery, ClaimDto>
{
    private readonly IClaimRepository _claimRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetClaimByIdQueryHandler> _logger;

    public GetClaimByIdQueryHandler(
        IClaimRepository claimRepository,
        ICacheService cacheService,
        ILogger<GetClaimByIdQueryHandler> logger)
    {
        _claimRepository = claimRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<ClaimDto> Handle(GetClaimByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Try group cache first
            const string groupKey = "admin:claims";
            var cacheKey = $"{groupKey}:{request.Id}";
            var cachedClaim = await _cacheService.GetAsync<ClaimDto>(cacheKey, cancellationToken);
            if (cachedClaim != null)
                return cachedClaim;

            // Cache miss - fetch from database
            var claim = await _claimRepository.GetByIdAsync(request.Id, cancellationToken);
            if (claim == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.ClaimNotFound);

            var claimDto = ClaimDto.MapFrom(claim);

            // Cache under group for 30 minutes
            await _cacheService.SetAsync(cacheKey, claimDto, TimeSpan.FromMinutes(30), cancellationToken);

            return claimDto;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting claim {ClaimId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
