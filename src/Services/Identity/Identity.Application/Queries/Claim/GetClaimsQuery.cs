using MediatR;
using Identity.Application.DTOs;

namespace Identity.Application.Queries.Claim;

public record GetClaimsQuery : IRequest<List<ClaimDto>>;

public record GetClaimByIdQuery(int Id) : IRequest<ClaimDto>;
