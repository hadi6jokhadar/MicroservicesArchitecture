using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using Identity.Application.Queries.Claim;
using Identity.Application.DTOs;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Handlers.Admin.Claim;

public class GetClaimsQueryHandler : IRequestHandler<GetClaimsQuery, List<ClaimDto>>
{
    private readonly IClaimRepository _claimRepository;
    private readonly ICacheService _cacheService;

    public GetClaimsQueryHandler(
        IClaimRepository claimRepository,
        ICacheService cacheService)
    {
        _claimRepository = claimRepository;
        _cacheService = cacheService;
    }

    public async Task<List<ClaimDto>> Handle(GetClaimsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Try cache first
            var cachedClaims = await _cacheService.GetAsync<List<ClaimDto>>("claims_all", cancellationToken);
            if (cachedClaims != null)
                return cachedClaims;

            // Cache miss - fetch from database
            var claims = await _claimRepository.GetAllAsync(false, cancellationToken);
            var claimDtos = claims.Select(ClaimDto.MapFrom).ToList();

            // Cache for 30 minutes
            await _cacheService.SetAsync("claims_all", claimDtos, TimeSpan.FromMinutes(30), cancellationToken);

            return claimDtos;
        }
        catch (Exception)
        {
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}

public class GetClaimByIdQueryHandler : IRequestHandler<GetClaimByIdQuery, ClaimDto>
{
    private readonly IClaimRepository _claimRepository;
    private readonly ICacheService _cacheService;

    public GetClaimByIdQueryHandler(
        IClaimRepository claimRepository,
        ICacheService cacheService)
    {
        _claimRepository = claimRepository;
        _cacheService = cacheService;
    }

    public async Task<ClaimDto> Handle(GetClaimByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Try cache first
            var cacheKey = $"claim_{request.Id}";
            var cachedClaim = await _cacheService.GetAsync<ClaimDto>(cacheKey, cancellationToken);
            if (cachedClaim != null)
                return cachedClaim;

            // Cache miss - fetch from database
            var claim = await _claimRepository.GetByIdAsync(request.Id, cancellationToken);
            if (claim == null)
                throw new NotFoundException(LocalizationKeys.Exceptions.ClaimNotFound);

            var claimDto = ClaimDto.MapFrom(claim);

            // Cache for 30 minutes
            await _cacheService.SetAsync(cacheKey, claimDto, TimeSpan.FromMinutes(30), cancellationToken);

            return claimDto;
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
