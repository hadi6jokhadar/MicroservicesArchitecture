using FileManager.Application.Queries;
using FluentValidation;

namespace FileManager.Application.Handlers.GetFiles;

public class GetFilesQueryValidator : AbstractValidator<GetFilesQuery>
{
    public GetFilesQueryValidator()
    {
        RuleFor(x => x.Request.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0.");

        RuleFor(x => x.Request.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Request.SortBy)
            .Must(BeValidSortColumn!)
            .When(x => x.Request.SortBy != null)
            .WithMessage("Invalid sort column.");
    }

    private bool BeValidSortColumn(string? sortBy)
    {
        if (string.IsNullOrEmpty(sortBy))
            return true; // Allow null/empty, will use default

        var validColumns = new[] { "Id", "Name", "Extension", "Size", "Type", "Group", "Created", "LastModified" };
        return validColumns.Contains(sortBy, StringComparer.OrdinalIgnoreCase);
    }
}
