namespace IhsanDev.Shared.Application.Localization;

/// <summary>
/// Service for translating localization keys to localized strings
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Get localized string by key
    /// </summary>
    /// <param name="key">The localization key (e.g., "backend_exception_bad_request")</param>
    /// <returns>Localized string or key if translation not found</returns>
    string GetString(string key);

    /// <summary>
    /// Get localized string by key with parameters
    /// </summary>
    /// <param name="key">The localization key</param>
    /// <param name="args">Format arguments to replace placeholders (e.g., {0}, {1})</param>
    /// <returns>Formatted localized string or key if translation not found</returns>
    string GetString(string key, params object[] args);

    /// <summary>
    /// Get current culture code (e.g., "en", "ar")
    /// </summary>
    string GetCurrentCulture();

    /// <summary>
    /// Set current culture
    /// </summary>
    /// <param name="culture">Culture code (e.g., "en", "ar")</param>
    void SetCulture(string culture);

    /// <summary>
    /// Check if key exists in current localization
    /// </summary>
    bool HasKey(string key);
}
