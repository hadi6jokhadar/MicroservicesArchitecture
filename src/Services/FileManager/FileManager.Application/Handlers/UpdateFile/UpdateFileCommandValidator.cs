using FileManager.Application.Commands;
using FluentValidation;

namespace FileManager.Application.Handlers.UpdateFile;

public class UpdateFileCommandValidator : AbstractValidator<UpdateFileCommand>
{
    public UpdateFileCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("File ID must be greater than 0.");

        RuleFor(x => x.Group)
            .IsInEnum()
            .When(x => x.Group.HasValue)
            .WithMessage("Invalid file group.");

        RuleFor(x => x.Name)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.Name))
            .WithMessage("File name cannot exceed 255 characters.");
    }
}
