using FluentValidation;
using Identity.Application.Commands.DeviceToken;

namespace Identity.Application.Validators.DeviceToken;

public class AddDeviceTokenCommandValidator : AbstractValidator<AddDeviceTokenCommand>
{
    public AddDeviceTokenCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("UserId must be greater than 0");

        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Token is required")
            .MaximumLength(500)
            .WithMessage("Token cannot exceed 500 characters");

        RuleFor(x => x.Platform)
            .IsInEnum()
            .WithMessage("Invalid platform value");

        RuleFor(x => x.DeviceIdentifier)
            .MaximumLength(100)
            .WithMessage("DeviceIdentifier cannot exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.DeviceIdentifier));
    }
}

public class UpdateDeviceTokenCommandValidator : AbstractValidator<UpdateDeviceTokenCommand>
{
    public UpdateDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0");

        RuleFor(x => x.Token)
            .MaximumLength(500)
            .WithMessage("Token cannot exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Token));

        RuleFor(x => x.DeviceIdentifier)
            .MaximumLength(100)
            .WithMessage("DeviceIdentifier cannot exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.DeviceIdentifier));
    }
}

public class DeleteDeviceTokenCommandValidator : AbstractValidator<DeleteDeviceTokenCommand>
{
    public DeleteDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0");
    }
}

public class DeleteAllUserDeviceTokensCommandValidator : AbstractValidator<DeleteAllUserDeviceTokensCommand>
{
    public DeleteAllUserDeviceTokensCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("UserId must be greater than 0");
    }
}
