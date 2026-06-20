using IhsanDev.Shared.Application.Constants;
using IhsanDev.Shared.Infrastructure.Services.Cache;
using IhsanDev.Shared.Kernel.Dto.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Tenant.API.Tests.Infrastructure;
using Tenant.Application.Commands.Tenant;
using Tenant.Domain.Entities;

namespace Tenant.API.Tests.Endpoints;

[Collection("Sequential")]
public class FeatureFlagsEndpointsTests : IntegrationTestBase
{
    public FeatureFlagsEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    private static string FlagsCacheKey(string tenantId) => $"tenant_feature_flags_{tenantId}";

    private async Task<Dictionary<string, bool>?> GetFromCacheAsync(string tenantId)
    {
        using var scope = Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        return await cache.GetAsync<Dictionary<string, bool>>(FlagsCacheKey(tenantId));
    }

    private async Task ClearFlagsCacheAsync(string tenantId)
    {
        using var scope = Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cache.RemoveAsync(FlagsCacheKey(tenantId));
    }

    private async Task SeedCacheAsync(string tenantId, Dictionary<string, bool> flags)
    {
        using var scope = Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cache.SetAsync(FlagsCacheKey(tenantId), flags, TimeSpan.FromDays(7));
    }

    #region Default Flags — No TenantId

    [Fact]
    public async Task GetFeatureFlags_WithNullTenantId_ShouldReturnAllDefaultsEnabled()
    {
        var result = await SendAsync(new GetTenantFeatureFlagsQuery(null));

        result.Should().HaveCount(4);
        result.Should().ContainKey(FeatureFlags.AiChatEnabled).WhoseValue.Should().BeTrue();
        result.Should().ContainKey(FeatureFlags.NasheedIngestionEnabled).WhoseValue.Should().BeTrue();
        result.Should().ContainKey(FeatureFlags.IsBackgroundJobPageEnabled).WhoseValue.Should().BeTrue();
        result.Should().ContainKey(FeatureFlags.IsAuditLogPageEnabled).WhoseValue.Should().BeTrue();
    }

    [Fact]
    public async Task GetFeatureFlags_WithEmptyTenantId_ShouldReturnDefaults()
    {
        var result = await SendAsync(new GetTenantFeatureFlagsQuery(string.Empty));

        result.Should().HaveCount(4);
        result.Should().OnlyContain(kv => kv.Value);
    }

    [Fact]
    public async Task GetFeatureFlags_WithWhitespaceTenantId_ShouldReturnDefaults()
    {
        var result = await SendAsync(new GetTenantFeatureFlagsQuery("   "));

        result.Should().HaveCount(4);
        result.Should().OnlyContain(kv => kv.Value);
    }

    #endregion

    #region DB Miss — Tenant Not Found in Database

    [Fact]
    public async Task GetFeatureFlags_ForNonExistentTenant_ShouldReturnDefaultsWithoutCaching()
    {
        var tenantId = GenerateUniqueTenantId("ff-notfound");
        await ClearFlagsCacheAsync(tenantId);

        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        result.Should().HaveCount(4);
        result.Should().OnlyContain(kv => kv.Value);

        // Handler skips SetAsync for non-existent tenants — cache must remain empty
        var cached = await GetFromCacheAsync(tenantId);
        cached.Should().BeNull("defaults for non-existent tenants must not be cached");
    }

    #endregion

    #region DB Fetch — Flag Deserialization and Merging

    [Fact]
    public async Task GetFeatureFlags_TenantWithNoFeatureFlagsInConfig_ShouldReturnAndCacheDefaults()
    {
        // TenantConfiguration with an empty FeatureFlags dictionary — no overrides
        var tenantId = GenerateUniqueTenantId("ff-empty-flags");
        await CreateTestTenantAsync(tenantId: tenantId, data: CreateDefaultTenantConfiguration());
        await ClearFlagsCacheAsync(tenantId);

        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        result.Should().HaveCount(4);
        result.Should().OnlyContain(kv => kv.Value);

        // Even with empty flags, tenant exists → result is cached
        var cached = await GetFromCacheAsync(tenantId);
        cached.Should().NotBeNull("defaults should be cached when the tenant exists in DB");
    }

    [Fact]
    public async Task GetFeatureFlags_TenantWithOneOverriddenFlag_ShouldMergeFlagOverDefaults()
    {
        var tenantId = GenerateUniqueTenantId("ff-one-override");
        var config = CreateDefaultTenantConfiguration();
        config.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: config);
        await ClearFlagsCacheAsync(tenantId);

        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        result.Should().ContainKey(FeatureFlags.AiChatEnabled).WhoseValue.Should().BeFalse();
        result.Should().ContainKey(FeatureFlags.NasheedIngestionEnabled).WhoseValue.Should().BeTrue();
        result.Should().ContainKey(FeatureFlags.IsBackgroundJobPageEnabled).WhoseValue.Should().BeTrue();
        result.Should().ContainKey(FeatureFlags.IsAuditLogPageEnabled).WhoseValue.Should().BeTrue();
    }

    [Fact]
    public async Task GetFeatureFlags_TenantWithAllFlagsDisabled_ShouldReturnAllFalse()
    {
        var tenantId = GenerateUniqueTenantId("ff-all-off");
        var config = CreateDefaultTenantConfiguration();
        config.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false,
            [FeatureFlags.NasheedIngestionEnabled] = false,
            [FeatureFlags.IsBackgroundJobPageEnabled] = false,
            [FeatureFlags.IsAuditLogPageEnabled] = false
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: config);
        await ClearFlagsCacheAsync(tenantId);

        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        result.Should().HaveCount(4);
        result.Should().NotContain(kv => kv.Value);
    }

    [Fact]
    public async Task GetFeatureFlags_TenantWithCustomFlag_ShouldIncludeItBesideDefaults()
    {
        // Tenant defines an app-specific flag not in the system default set
        var tenantId = GenerateUniqueTenantId("ff-custom-flag");
        var config = CreateDefaultTenantConfiguration();
        config.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false,
            ["myAppSpecificFeature"] = true
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: config);
        await ClearFlagsCacheAsync(tenantId);

        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        result.Should().ContainKey(FeatureFlags.AiChatEnabled).WhoseValue.Should().BeFalse();
        result.Should().ContainKey("myAppSpecificFeature").WhoseValue.Should().BeTrue();
        result.Should().ContainKey(FeatureFlags.NasheedIngestionEnabled).WhoseValue.Should().BeTrue();
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetFeatureFlags_TenantWithInvalidJsonData_ShouldReturnDefaultsWithoutThrowing()
    {
        var tenantId = GenerateUniqueTenantId("ff-bad-json");
        await ExecuteDbContextAsync(async context =>
        {
            context.TenantSettings.Add(new TenantSettings
            {
                TenantId = tenantId,
                TenantName = "Bad JSON Tenant",
                UserId = GenerateUniqueUserId(),
                StartDate = DateTime.UtcNow,
                ExpireDate = DateTime.UtcNow.AddYears(1),
                Data = "{ this is: not valid json {{{{",
                IsActive = true,
                Created = DateTime.UtcNow,
                IsArchived = false
            });
            await context.SaveChangesAsync();
        });
        await ClearFlagsCacheAsync(tenantId);

        // Must never throw — falls back to defaults on any deserialization error
        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        result.Should().HaveCount(4);
        result.Should().OnlyContain(kv => kv.Value);
    }

    [Fact]
    public async Task GetFeatureFlags_TenantWithEmptyJsonData_ShouldReturnDefaults()
    {
        var tenantId = GenerateUniqueTenantId("ff-empty-json");
        await ExecuteDbContextAsync(async context =>
        {
            context.TenantSettings.Add(new TenantSettings
            {
                TenantId = tenantId,
                TenantName = "Empty Data Tenant",
                UserId = GenerateUniqueUserId(),
                StartDate = DateTime.UtcNow,
                ExpireDate = DateTime.UtcNow.AddYears(1),
                Data = "{}",
                IsActive = true,
                Created = DateTime.UtcNow,
                IsArchived = false
            });
            await context.SaveChangesAsync();
        });
        await ClearFlagsCacheAsync(tenantId);

        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        result.Should().HaveCount(4);
        result.Should().OnlyContain(kv => kv.Value);
    }

    #endregion

    #region Cache Population — First Query Seeds the Cache

    [Fact]
    public async Task GetFeatureFlags_OnFirstQuery_ShouldPopulateCacheWithMergedResult()
    {
        var tenantId = GenerateUniqueTenantId("ff-cache-pop");
        var config = CreateDefaultTenantConfiguration();
        config.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: config);
        await ClearFlagsCacheAsync(tenantId);

        // First query triggers DB fetch
        await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        // Cache must now contain the merged flags
        var cached = await GetFromCacheAsync(tenantId);
        cached.Should().NotBeNull("cache should be populated after first query");
        cached!.Should().ContainKey(FeatureFlags.AiChatEnabled).WhoseValue.Should().BeFalse();
        cached.Should().ContainKey(FeatureFlags.NasheedIngestionEnabled).WhoseValue.Should().BeTrue();
        cached.Should().ContainKey(FeatureFlags.IsBackgroundJobPageEnabled).WhoseValue.Should().BeTrue();
        cached.Should().ContainKey(FeatureFlags.IsAuditLogPageEnabled).WhoseValue.Should().BeTrue();
    }

    #endregion

    #region Cache Hit — Subsequent Queries Use Cache

    [Fact]
    public async Task GetFeatureFlags_WhenCachePreSeededWithDifferentValues_ShouldReturnCachedNotDbValues()
    {
        // DB stores aiChatEnabled = true; we force cache to have false
        var tenantId = GenerateUniqueTenantId("ff-cache-hit");
        var config = CreateDefaultTenantConfiguration();
        config.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = true
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: config);

        await SeedCacheAsync(tenantId, new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false,   // deliberately opposite of DB
            [FeatureFlags.NasheedIngestionEnabled] = false,
            [FeatureFlags.IsBackgroundJobPageEnabled] = true,
            [FeatureFlags.IsAuditLogPageEnabled] = true
        });

        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        // Cache wins over DB
        result.Should().ContainKey(FeatureFlags.AiChatEnabled).WhoseValue.Should().BeFalse(
            "cache hit must return cached value even when it differs from DB");
        result.Should().ContainKey(FeatureFlags.NasheedIngestionEnabled).WhoseValue.Should().BeFalse();
    }

    [Fact]
    public async Task GetFeatureFlags_CalledMultipleTimes_ShouldReturnConsistentResults()
    {
        var tenantId = GenerateUniqueTenantId("ff-idempotent");
        var config = CreateDefaultTenantConfiguration();
        config.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false,
            [FeatureFlags.IsAuditLogPageEnabled] = false
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: config);
        await ClearFlagsCacheAsync(tenantId);

        // First call: DB fetch + cache write. Subsequent calls: cache read.
        var result1 = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));
        var result2 = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));
        var result3 = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        result1.Should().BeEquivalentTo(result2);
        result2.Should().BeEquivalentTo(result3);
        result1[FeatureFlags.AiChatEnabled].Should().BeFalse();
        result1[FeatureFlags.IsAuditLogPageEnabled].Should().BeFalse();
        result1[FeatureFlags.NasheedIngestionEnabled].Should().BeTrue();
    }

    #endregion

    #region Cache Invalidation — After Update

    [Fact]
    public async Task GetFeatureFlags_AfterTenantUpdate_ShouldClearFeatureFlagsCacheEntry()
    {
        var tenantId = GenerateUniqueTenantId("ff-upd-clear");
        await CreateTestTenantAsync(tenantId: tenantId, data: CreateDefaultTenantConfiguration());
        await ClearFlagsCacheAsync(tenantId);

        // Populate cache
        await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));
        (await GetFromCacheAsync(tenantId)).Should().NotBeNull("pre-condition: cache should exist before update");

        // UpdateTenant explicitly removes tenant_feature_flags_{tenantId} from cache
        var updatedConfig = CreateDefaultTenantConfiguration();
        updatedConfig.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false
        };
        await SendAsync(new UpdateTenantCommand(
            TenantId: tenantId,
            TenantName: "Updated Tenant",
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: updatedConfig,
            IsActive: true
        ));

        var cached = await GetFromCacheAsync(tenantId);
        cached.Should().BeNull("UpdateTenant must remove the feature flags cache entry");
    }

    [Fact]
    public async Task GetFeatureFlags_AfterTenantUpdate_ShouldReturnUpdatedFlagsFromDb()
    {
        var tenantId = GenerateUniqueTenantId("ff-upd-reflect");
        var initialConfig = CreateDefaultTenantConfiguration();
        initialConfig.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false,
            [FeatureFlags.NasheedIngestionEnabled] = false
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: initialConfig);
        await ClearFlagsCacheAsync(tenantId);

        // First query — DB fetch, caches {aiChat=false, nasheed=false}
        var before = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));
        before[FeatureFlags.AiChatEnabled].Should().BeFalse();
        before[FeatureFlags.NasheedIngestionEnabled].Should().BeFalse();

        // Update flips both flags
        var updatedConfig = CreateDefaultTenantConfiguration();
        updatedConfig.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = true,
            [FeatureFlags.NasheedIngestionEnabled] = true
        };
        await SendAsync(new UpdateTenantCommand(
            TenantId: tenantId,
            TenantName: "Flags Flipped Tenant",
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: updatedConfig,
            IsActive: true
        ));

        // Second query — cache was cleared by update, so fetches fresh data from DB
        var after = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        after[FeatureFlags.AiChatEnabled].Should().BeTrue();
        after[FeatureFlags.NasheedIngestionEnabled].Should().BeTrue();
    }

    [Fact]
    public async Task GetFeatureFlags_AfterTenantUpdate_ShouldRepopulateCacheWithNewValues()
    {
        var tenantId = GenerateUniqueTenantId("ff-repopulate");
        var initialConfig = CreateDefaultTenantConfiguration();
        initialConfig.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.IsAuditLogPageEnabled] = false
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: initialConfig);
        await ClearFlagsCacheAsync(tenantId);

        // First query — populates cache with initial values
        await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        // Update with new flag values
        var updatedConfig = CreateDefaultTenantConfiguration();
        updatedConfig.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.IsAuditLogPageEnabled] = true,
            [FeatureFlags.IsBackgroundJobPageEnabled] = false
        };
        await SendAsync(new UpdateTenantCommand(
            TenantId: tenantId,
            TenantName: "Repopulate Cache Tenant",
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: updatedConfig,
            IsActive: true
        ));

        // Second query after update — cache miss → DB fetch → re-populates cache
        await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        var recached = await GetFromCacheAsync(tenantId);
        recached.Should().NotBeNull("cache should be re-populated after post-update query");
        recached![FeatureFlags.IsAuditLogPageEnabled].Should().BeTrue();
        recached[FeatureFlags.IsBackgroundJobPageEnabled].Should().BeFalse();
    }

    #endregion

    #region Cache Behavior — After Soft Delete

    [Fact]
    public async Task GetFeatureFlags_AfterSoftDeleteWithCacheCleared_ShouldReturnDefaults()
    {
        // With an explicit cache clear, the handler sees the tenant is gone and returns defaults
        var tenantId = GenerateUniqueTenantId("ff-del-clean");
        var config = CreateDefaultTenantConfiguration();
        config.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: config);
        await ClearFlagsCacheAsync(tenantId);

        // Soft-delete (DeleteTenant removes tenant_config_* but NOT tenant_feature_flags_*)
        await SendAsync(new DeleteTenantCommand(tenantId));

        // Manually clear the stale feature flags cache entry
        await ClearFlagsCacheAsync(tenantId);

        // Query: tenant is archived → GetByTenantIdAsync returns null → handler returns defaults
        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));

        result.Should().OnlyContain(kv => kv.Value,
            "soft-deleted tenant is invisible to GetByTenantIdAsync; defaults are returned");
    }

    [Fact]
    public async Task GetFeatureFlags_AfterSoftDelete_StaleFeatureFlagsCacheIsNotCleared()
    {
        // Documents that DeleteTenant does NOT clear tenant_feature_flags_* cache.
        // Stale flags remain cached until the 7-day TTL expires naturally.
        var tenantId = GenerateUniqueTenantId("ff-stale");
        var config = CreateDefaultTenantConfiguration();
        config.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false
        };
        await CreateTestTenantAsync(tenantId: tenantId, data: config);
        await ClearFlagsCacheAsync(tenantId);

        // Populate the feature flags cache before delete
        await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));
        (await GetFromCacheAsync(tenantId)).Should().NotBeNull("pre-condition: cache populated");

        // Soft-delete — only clears tenant_config_*, leaves tenant_feature_flags_* intact
        await SendAsync(new DeleteTenantCommand(tenantId));

        // Cache entry must still be present (not cleared by delete)
        var cacheAfterDelete = await GetFromCacheAsync(tenantId);
        cacheAfterDelete.Should().NotBeNull(
            "DeleteTenant does not clear the feature flags cache; stale entry persists until TTL");

        // Querying after delete returns the stale cached value, not defaults
        var result = await SendAsync(new GetTenantFeatureFlagsQuery(tenantId));
        result[FeatureFlags.AiChatEnabled].Should().BeFalse(
            "stale cache hit returns the pre-delete flag values");
    }

    #endregion

    #region Tenant Isolation — Separate Cache Entries Per Tenant

    [Fact]
    public async Task GetFeatureFlags_ForDifferentTenants_ShouldUseSeparateCacheEntries()
    {
        var tenantIdA = GenerateUniqueTenantId("ff-iso-a");
        var tenantIdB = GenerateUniqueTenantId("ff-iso-b");

        var configA = CreateDefaultTenantConfiguration();
        configA.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = false
        };

        var configB = CreateDefaultTenantConfiguration();
        configB.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.AiChatEnabled] = true
        };

        await CreateTestTenantAsync(tenantId: tenantIdA, data: configA);
        await CreateTestTenantAsync(tenantId: tenantIdB, data: configB);
        await ClearFlagsCacheAsync(tenantIdA);
        await ClearFlagsCacheAsync(tenantIdB);

        var resultA = await SendAsync(new GetTenantFeatureFlagsQuery(tenantIdA));
        var resultB = await SendAsync(new GetTenantFeatureFlagsQuery(tenantIdB));

        // Each tenant gets its own flag values
        resultA[FeatureFlags.AiChatEnabled].Should().BeFalse();
        resultB[FeatureFlags.AiChatEnabled].Should().BeTrue();

        // Each tenant has its own cache entry
        var cacheA = await GetFromCacheAsync(tenantIdA);
        var cacheB = await GetFromCacheAsync(tenantIdB);
        cacheA![FeatureFlags.AiChatEnabled].Should().BeFalse();
        cacheB![FeatureFlags.AiChatEnabled].Should().BeTrue();
    }

    [Fact]
    public async Task GetFeatureFlags_UpdateOneTenant_ShouldNotAffectOtherTenantCache()
    {
        var tenantIdA = GenerateUniqueTenantId("ff-upd-iso-a");
        var tenantIdB = GenerateUniqueTenantId("ff-upd-iso-b");

        var configA = CreateDefaultTenantConfiguration();
        configA.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.NasheedIngestionEnabled] = false
        };
        var configB = CreateDefaultTenantConfiguration();
        configB.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.NasheedIngestionEnabled] = false
        };

        await CreateTestTenantAsync(tenantId: tenantIdA, data: configA);
        await CreateTestTenantAsync(tenantId: tenantIdB, data: configB);
        await ClearFlagsCacheAsync(tenantIdA);
        await ClearFlagsCacheAsync(tenantIdB);

        // Populate both caches
        await SendAsync(new GetTenantFeatureFlagsQuery(tenantIdA));
        await SendAsync(new GetTenantFeatureFlagsQuery(tenantIdB));

        // Update only tenant A
        var updatedConfigA = CreateDefaultTenantConfiguration();
        updatedConfigA.FeatureFlags = new Dictionary<string, bool>
        {
            [FeatureFlags.NasheedIngestionEnabled] = true
        };
        await SendAsync(new UpdateTenantCommand(
            TenantId: tenantIdA,
            TenantName: "Updated Tenant A",
            StartDate: DateTime.UtcNow,
            ExpireDate: DateTime.UtcNow.AddYears(1),
            Data: updatedConfigA,
            IsActive: true
        ));

        // Tenant A's cache was cleared; tenant B's cache must be untouched
        var cacheA = await GetFromCacheAsync(tenantIdA);
        var cacheB = await GetFromCacheAsync(tenantIdB);

        cacheA.Should().BeNull("updating tenant A must clear only tenant A's cache");
        cacheB.Should().NotBeNull("tenant B's cache must remain intact");
        cacheB![FeatureFlags.NasheedIngestionEnabled].Should().BeFalse();
    }

    #endregion
}
