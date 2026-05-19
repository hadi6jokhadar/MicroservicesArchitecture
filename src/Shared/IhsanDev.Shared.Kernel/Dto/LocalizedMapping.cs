namespace IhsanDev.Shared.Kernel.Dto;

/// <summary>
/// Represents a locale-to-value mapping stored as JSONB.
/// Example: { "en": "Electronics", "ar": "إلكترونيات", "tr": "Elektronik" }
/// </summary>
public sealed class LocalizedMapping
{
    private readonly Dictionary<string, string> _translations;

    public LocalizedMapping()
    {
        _translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public LocalizedMapping(Dictionary<string, string> translations)
    {
        _translations = new Dictionary<string, string>(translations, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the underlying translations dictionary.</summary>
    public IReadOnlyDictionary<string, string> Translations => _translations;

    /// <summary>Returns translation for the given locale, or null if not found.</summary>
    public string? Get(string locale) =>
        _translations.TryGetValue(locale, out var value) ? value : null;

    /// <summary>Sets a translation for the given locale.</summary>
    public void Set(string locale, string value) => _translations[locale] = value;

    /// <summary>Returns translation for locale or falls back to English or the first available value.</summary>
    public string GetOrFallback(string locale = "en") =>
        Get(locale)
        ?? Get("en")
        ?? (_translations.Count > 0 ? _translations.Values.First() : string.Empty);

    /// <summary>Creates a LocalizedMapping from a plain dictionary.</summary>
    public static LocalizedMapping From(Dictionary<string, string> translations) =>
        new(translations);

    /// <summary>Creates an empty LocalizedMapping.</summary>
    public static LocalizedMapping Empty => new();
}
