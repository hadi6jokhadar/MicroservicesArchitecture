using FluentValidation;
using IhsanDev.Shared.Application.Localization;

namespace IhsanDev.Shared.Application.Validation;

/// <summary>
/// Extension methods for FluentValidation to support localization
/// </summary>
public static class LocalizedValidationExtensions
{
    /// <summary>
    /// Set localized error message using localization key
    /// </summary>
    public static IRuleBuilderOptions<T, TProperty> WithLocalizedMessage<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder,
        string localizationKey,
        ILocalizationService localizationService)
    {
        return ruleBuilder.WithMessage(localizationService.GetString(localizationKey));
    }

    /// <summary>
    /// Set localized error message using localization key with format arguments
    /// </summary>
    public static IRuleBuilderOptions<T, TProperty> WithLocalizedMessage<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder,
        string localizationKey,
        ILocalizationService localizationService,
        params object[] args)
    {
        return ruleBuilder.WithMessage(localizationService.GetString(localizationKey, args));
    }

    /// <summary>
    /// Set property name from localization (for {PropertyName} placeholder)
    /// </summary>
    public static IRuleBuilderOptions<T, TProperty> WithLocalizedPropertyName<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder,
        string propertyNameKey,
        ILocalizationService localizationService)
    {
        return ruleBuilder.WithName(localizationService.GetString(propertyNameKey));
    }
}

/// <summary>
/// Base validator with built-in localization support
/// </summary>
public abstract class LocalizedValidator<T> : AbstractValidator<T>
{
    protected readonly ILocalizationService LocalizationService;

    protected LocalizedValidator(ILocalizationService localizationService)
    {
        LocalizationService = localizationService;
    }

    /// <summary>
    /// Get localized string by key
    /// </summary>
    protected string L(string key) => LocalizationService.GetString(key);

    /// <summary>
    /// Get localized string by key with format arguments
    /// </summary>
    protected string L(string key, params object[] args) => LocalizationService.GetString(key, args);
}
