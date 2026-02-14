using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using MediatR;
using Translation.Application.DTOs;

namespace Translation.Application.Queries;

/// <summary>
/// Query to get paginated list of translation keys
/// Supports filtering by category and tenantId
/// </summary>
public record GetTranslationKeysQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Category = null,
    string? TenantId = null,
    string? SearchTerm = null,
    bool IsArchived = false
) : IRequest<IhsanDev.Shared.Application.Common.Models.PaginatedList<TranslationKeyDto>>;

public class GetTranslationKeysQueryValidator : LocalizedValidator<GetTranslationKeysQuery>
{
    public GetTranslationKeysQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.PageNumber), "0"));

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, L(LocalizationKeys.Fields.PageSize), "0"))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.MustBeLessThanOrEqual, L(LocalizationKeys.Fields.PageSize), "100"));
    }
}
