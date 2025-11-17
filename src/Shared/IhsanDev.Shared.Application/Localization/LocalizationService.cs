using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Application.Localization;

/// <summary>
/// Default implementation of ILocalizationService using JSON resource files
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocalizationService> _logger;
    private readonly string _resourcesPath;
    private const string DefaultCulture = "en";
    private const string CacheKeyPrefix = "Localization_";

    public LocalizationService(
        IMemoryCache cache,
        ILogger<LocalizationService> logger,
        string? resourcesPath = null)
    {
        _cache = cache;
        _logger = logger;
        
        // Default resources path relative to application directory
        _resourcesPath = resourcesPath ?? 
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Localization");
    }

    public string GetString(string key)
    {
        try
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var translations = LoadTranslations(culture);

            if (translations.TryGetValue(key, out var value))
            {
                return value;
            }

            // Fallback to default culture
            if (culture != DefaultCulture)
            {
                var defaultTranslations = LoadTranslations(DefaultCulture);
                if (defaultTranslations.TryGetValue(key, out var defaultValue))
                {
                    _logger.LogWarning(
                        "Translation key '{Key}' not found in culture '{Culture}', using default culture '{DefaultCulture}'",
                        key, culture, DefaultCulture);
                    return defaultValue;
                }
            }

            _logger.LogWarning("Translation key '{Key}' not found in any culture", key);
            return key; // Return key if not found
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting localized string for key '{Key}'", key);
            return key;
        }
    }

    public string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, 
                "Error formatting localized string for key '{Key}' with arguments", key);
            return template;
        }
    }

    public string GetCurrentCulture()
    {
        return CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
    }

    public void SetCulture(string culture)
    {
        var cultureInfo = new CultureInfo(culture);
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
        Thread.CurrentThread.CurrentCulture = cultureInfo;
        Thread.CurrentThread.CurrentUICulture = cultureInfo;
    }

    public bool HasKey(string key)
    {
        var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        var translations = LoadTranslations(culture);
        return translations.ContainsKey(key);
    }

    private Dictionary<string, string> LoadTranslations(string culture)
    {
        var cacheKey = $"{CacheKeyPrefix}{culture}";
        
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SetAbsoluteExpiration(TimeSpan.FromHours(24)); // Cache for 24 hours
            
            var filePath = Path.Combine(_resourcesPath, $"{culture}.json");
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning(
                    "Localization file not found: {FilePath}. Creating default directory structure.",
                    filePath);
                
                // Create directory if it doesn't exist
                Directory.CreateDirectory(_resourcesPath);
                
                return new Dictionary<string, string>();
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                
                _logger.LogInformation(
                    "Loaded {Count} translations for culture '{Culture}'",
                    translations?.Count ?? 0, culture);
                
                return translations ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error loading localization file: {FilePath}", filePath);
                return new Dictionary<string, string>();
            }
        }) ?? new Dictionary<string, string>();
    }
}
