using MediatR;
using Translation.Application.DTOs;

namespace Translation.Application.Queries;

/// <summary>
/// Query to get translations for a specific language and optionally for a specific tenant
/// If Category is provided, only translations from that category will be returned
/// </summary>
public record GetTranslationsQuery(
    string Language,
    string? TenantId = null,
    string? Category = null
) : IRequest<TranslationsDto>;
