using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;
using Nasheed.Application.Commands;
using Nasheed.Application.Queries;
using Nasheed.Domain.Entities;

namespace Nasheed.Application.Validators;

public class CreateSongCommandValidator : LocalizedValidator<CreateSongCommand>
{
    public CreateSongCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.ArtistId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.Required, "ArtistId"));

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(L(LocalizationKeys.Validation.Required, "Title"))
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Title", 500));

        RuleFor(x => x.FileId)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.Required, "FileId"));

        RuleFor(x => x.CopyrightRiskLevel)
            .Must(BeValidRiskLevel)
            .WithMessage(L(LocalizationKeys.Validation.InvalidFormat, "CopyrightRiskLevel"))
            .When(x => !string.IsNullOrWhiteSpace(x.CopyrightRiskLevel));

        RuleFor(x => x.ContentSafetyFlag)
            .Must(BeValidSafetyFlag)
            .WithMessage(L(LocalizationKeys.Validation.InvalidFormat, "ContentSafetyFlag"))
            .When(x => !string.IsNullOrWhiteSpace(x.ContentSafetyFlag));

        RuleFor(x => x.RiskReason)
            .MaximumLength(1000).WithMessage(L(LocalizationKeys.Validation.MaxLength, "RiskReason", 1000))
            .When(x => x.RiskReason != null);

        RuleFor(x => x)
            .Must(x => IsRiskPairComplete(x.CopyrightRiskLevel, x.ContentSafetyFlag))
            .WithMessage(L(LocalizationKeys.Validation.Required, "LegalCompliance"))
            .When(x => !string.IsNullOrWhiteSpace(x.CopyrightRiskLevel) || !string.IsNullOrWhiteSpace(x.ContentSafetyFlag));
    }

    private static bool BeValidRiskLevel(string? value)
    {
        var normalized = Normalize(value);
        return LegalComplianceEntity.IsValidRiskLevel(normalized);
    }

    private static bool BeValidSafetyFlag(string? value)
    {
        var normalized = Normalize(value);
        return LegalComplianceEntity.IsValidSafetyFlag(normalized);
    }

    private static bool IsRiskPairComplete(string? riskLevel, string? safetyFlag)
    {
        var hasRiskLevel = !string.IsNullOrWhiteSpace(riskLevel);
        var hasSafetyFlag = !string.IsNullOrWhiteSpace(safetyFlag);
        return hasRiskLevel == hasSafetyFlag;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant();
    }
}

public class UpdateSongCommandValidator : LocalizedValidator<UpdateSongCommand>
{
    public UpdateSongCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.Required, "Id"));

        RuleFor(x => x.Title)
            .MaximumLength(500).WithMessage(L(LocalizationKeys.Validation.MaxLength, "Title", 500))
            .When(x => x.Title != null);

        RuleFor(x => x.CopyrightRiskLevel)
            .Must(BeValidRiskLevel)
            .WithMessage(L(LocalizationKeys.Validation.InvalidFormat, "CopyrightRiskLevel"))
            .When(x => !string.IsNullOrWhiteSpace(x.CopyrightRiskLevel));

        RuleFor(x => x.ContentSafetyFlag)
            .Must(BeValidSafetyFlag)
            .WithMessage(L(LocalizationKeys.Validation.InvalidFormat, "ContentSafetyFlag"))
            .When(x => !string.IsNullOrWhiteSpace(x.ContentSafetyFlag));

        RuleFor(x => x.RiskReason)
            .MaximumLength(1000).WithMessage(L(LocalizationKeys.Validation.MaxLength, "RiskReason", 1000))
            .When(x => x.RiskReason != null);

        RuleFor(x => x)
            .Must(x => IsRiskPairComplete(x.CopyrightRiskLevel, x.ContentSafetyFlag))
            .WithMessage(L(LocalizationKeys.Validation.Required, "LegalCompliance"))
            .When(x => !string.IsNullOrWhiteSpace(x.CopyrightRiskLevel) || !string.IsNullOrWhiteSpace(x.ContentSafetyFlag));
    }

    private static bool BeValidRiskLevel(string? value)
    {
        var normalized = Normalize(value);
        return LegalComplianceEntity.IsValidRiskLevel(normalized);
    }

    private static bool BeValidSafetyFlag(string? value)
    {
        var normalized = Normalize(value);
        return LegalComplianceEntity.IsValidSafetyFlag(normalized);
    }

    private static bool IsRiskPairComplete(string? riskLevel, string? safetyFlag)
    {
        var hasRiskLevel = !string.IsNullOrWhiteSpace(riskLevel);
        var hasSafetyFlag = !string.IsNullOrWhiteSpace(safetyFlag);
        return hasRiskLevel == hasSafetyFlag;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant();
    }
}

public class GetSongListQueryValidator : LocalizedValidator<GetSongListQuery>
{
    public GetSongListQueryValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThan, "PageNumber", 0));
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage(L(LocalizationKeys.Validation.MustBeGreaterThanOrEqual, "PageSize", 1))
            .LessThanOrEqualTo(100).WithMessage(L(LocalizationKeys.Validation.MustBeLessThanOrEqual, "PageSize", 100));
    }
}
