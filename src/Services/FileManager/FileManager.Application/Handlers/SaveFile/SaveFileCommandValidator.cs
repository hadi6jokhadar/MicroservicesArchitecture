using FileManager.Application.Commands;
using FluentValidation;

namespace FileManager.Application.Handlers.SaveFile;

public class SaveFileCommandValidator : AbstractValidator<SaveFileCommand>
{
    public SaveFileCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required.");

        RuleFor(x => x.Group)
            .IsInEnum()
            .WithMessage("Invalid file group.");
    }
}
