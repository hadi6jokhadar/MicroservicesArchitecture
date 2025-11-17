using FileManager.Application.Queries;
using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;

namespace FileManager.Application.Handlers.GetFiles;

public class GetFilesQueryValidator : LocalizedValidator<GetFilesQuery>
{
    public GetFilesQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Request.PageNumber)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "Page number", "0"));

        RuleFor(x => x.Request.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .WithMessage(L(LocalizationKeys.Validation.PageSizeExceeded));

        RuleFor(x => x.Request.SortBy)
            .Must(BeValidSortColumn!)
            .When(x => x.Request.SortBy != null)
            .WithMessage(L(LocalizationKeys.Validation.InvalidFormat, "Sort column"));
    }

    private bool BeValidSortColumn(string? sortBy)
    {
        if (string.IsNullOrEmpty(sortBy))
            return true; // Allow null/empty, will use default

        var validColumns = new[] { "Id", "Name", "Extension", "Size", "Type", "Group", "Created", "LastModified" };
        return validColumns.Contains(sortBy, StringComparer.OrdinalIgnoreCase);
    }
}
