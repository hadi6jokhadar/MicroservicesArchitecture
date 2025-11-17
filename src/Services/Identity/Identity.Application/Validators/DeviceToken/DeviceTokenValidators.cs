using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Identity.Application.Commands.DeviceToken;

namespace Identity.Application.Validators.DeviceToken;

public class AddDeviceTokenCommandValidator : LocalizedValidator<AddDeviceTokenCommand>
{
    public AddDeviceTokenCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "UserId", "0"));

        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage(L(LocalizationKeys.Validation.Required, "Token"))
            .MaximumLength(500)
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, "Token", "500"));

        RuleFor(x => x.Platform)
            .IsInEnum()
            .WithMessage(L(LocalizationKeys.Validation.InvalidPlatform));

        RuleFor(x => x.DeviceIdentifier)
            .MaximumLength(100)
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, "DeviceIdentifier", "100"))
            .When(x => !string.IsNullOrWhiteSpace(x.DeviceIdentifier));
    }
}

public class UpdateDeviceTokenCommandValidator : LocalizedValidator<UpdateDeviceTokenCommand>
{
    public UpdateDeviceTokenCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "Id", "0"));

        RuleFor(x => x.Token)
            .MaximumLength(500)
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, "Token", "500"))
            .When(x => !string.IsNullOrWhiteSpace(x.Token));

        RuleFor(x => x.DeviceIdentifier)
            .MaximumLength(100)
            .WithMessage(L(LocalizationKeys.Validation.MaxLength, "DeviceIdentifier", "100"))
            .When(x => !string.IsNullOrWhiteSpace(x.DeviceIdentifier));
    }
}

public class DeleteDeviceTokenCommandValidator : LocalizedValidator<DeleteDeviceTokenCommand>
{
    public DeleteDeviceTokenCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "Id", "0"));
    }
}

public class DeleteAllUserDeviceTokensCommandValidator : LocalizedValidator<DeleteAllUserDeviceTokensCommand>
{
    public DeleteAllUserDeviceTokensCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "UserId", "0"));
    }
}
