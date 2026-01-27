using System.Net.Http.Headers;
using Translation.Domain.Entities;
using Translation.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Translation.API.Tests.Infrastructure;

/// <summary>
/// Base class for Translation API integration tests
/// Inherits from shared testing base and adds Translation-specific helpers
/// </summary>
public abstract class IntegrationTestBase : 
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<TranslationDbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
        // Note: Setting UsePostgreSQL here is too late - factory is already configured
        // To use PostgreSQL, override in CustomWebApplicationFactory constructor instead
    }
    
    /// <summary>
    /// Clear the distributed cache before a test runs
    /// Call this in tests that need fresh cache state
    /// </summary>
    protected async Task ClearCacheAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        
        // Clear all possible cache keys for English language
        var keysToClear = new[]
        {
            "translations:en:global:all",
            "translations:en:tenant-xyz:all",
            "translations:en:tenant-789:all",
            "translations:ar:global:all"
        };
        
        foreach (var key in keysToClear)
        {
            await cache.RemoveAsync(key);
        }
    }

    /// <summary>
    /// Create a test translation key with unique identifier
    /// </summary>
    protected async Task<TranslationKey> CreateTestTranslationKeyAsync(
        string? key = null,
        string category = "general",
        string? description = null,
        bool isActive = true)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var translationKey = TranslationKey.Create(
                key ?? $"test_key_{Guid.NewGuid():N}",
                category,
                description
            );
            
            if (!isActive)
            {
                translationKey.Deactivate();
            }

            context.TranslationKeys.Add(translationKey);
            await context.SaveChangesAsync();
            
            return translationKey;
        });
    }

    /// <summary>
    /// Create a test translation value for a given key
    /// </summary>
    protected async Task<TranslationValue> CreateTestTranslationValueAsync(
        int translationKeyId,
        string language = "en",
        string value = "Test Value",
        string? tenantId = null)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var translationValue = tenantId == null
                ? TranslationValue.CreateGlobal(translationKeyId, language, value)
                : TranslationValue.CreateTenantOverride(translationKeyId, language, value, tenantId);

            context.TranslationValues.Add(translationValue);
            await context.SaveChangesAsync();
            
            return translationValue;
        });
    }

    /// <summary>
    /// Get a test JWT token for authenticated requests
    /// Note: Translation service uses shared JWT authentication
    /// In real scenarios, this would come from Identity Service
    /// </summary>
    protected async Task<string> GetAuthTokenAsync()
    {
        // For testing purposes, we'll generate a simple test token
        // In production, this would be obtained from Identity Service
        await Task.CompletedTask;
        
        // Use a mock token - in real tests with auth, you'd integrate with Identity Service
        return "test-jwt-token";
    }

    /// <summary>
    /// Create a complete translation set (key + values in multiple languages)
    /// </summary>
    protected async Task<(TranslationKey Key, List<TranslationValue> Values)> CreateCompleteTranslationAsync(
        string key,
        string category,
        Dictionary<string, string> languageValues,
        string? tenantId = null)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var translationKey = TranslationKey.Create(key, category);

            context.TranslationKeys.Add(translationKey);
            await context.SaveChangesAsync();

            var values = new List<TranslationValue>();
            foreach (var (language, value) in languageValues)
            {
                var translationValue = tenantId == null
                    ? TranslationValue.CreateGlobal(translationKey.Id, language, value)
                    : TranslationValue.CreateTenantOverride(translationKey.Id, language, value, tenantId);
                values.Add(translationValue);
            }

            context.TranslationValues.AddRange(values);
            await context.SaveChangesAsync();

            return (translationKey, values);
        });
    }
}
