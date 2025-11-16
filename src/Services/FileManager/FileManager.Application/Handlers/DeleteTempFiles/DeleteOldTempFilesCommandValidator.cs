using FileManager.Application.Commands;
using FluentValidation;

namespace FileManager.Application.Handlers.DeleteTempFiles;

public class DeleteOldTempFilesCommandValidator : AbstractValidator<DeleteOldTempFilesCommand>
{
    public DeleteOldTempFilesCommandValidator()
    {
        RuleFor(x => x.OlderThanDays)
            .GreaterThan(0)
            .WithMessage("OlderThanDays must be greater than 0.");
    }
}
