using Identity.Infrastructure.Services;

namespace Identity.API.Tests.Seeding;

/// <summary>
/// Pure unit tests for <see cref="DatabaseSeeder.AppliesToTenant"/> — no database, no DI, no
/// factory. Covers the tenant-matching logic that decides whether a <see cref="Identity.Infrastructure.Seeding.SystemPermissionCatalog"/>
/// entry (like Nasheed's "anashid"-only role/claims) gets seeded for the current tenant. The
/// integration-level "does it actually skip Nasheed's entries" check lives in
/// <see cref="DatabaseSeederTests.SeedDefaultRolesAndClaimsAsync_WithoutMatchingTenant_ShouldNotSeedNasheedTenantScopedEntries"/> —
/// this file only exercises this method's decision logic, not the DB effect of that decision.
/// </summary>
public class DatabaseSeederTenantScopingTests
{
    [Fact]
    public void AppliesToTenant_NullTenantIds_IsPlatformWide_ShouldAlwaysApply()
    {
        DatabaseSeeder.AppliesToTenant(null, "anashid").Should().BeTrue();
        DatabaseSeeder.AppliesToTenant(null, null).Should().BeTrue();
    }

    [Fact]
    public void AppliesToTenant_EmptyTenantIds_IsPlatformWide_ShouldAlwaysApply()
    {
        DatabaseSeeder.AppliesToTenant([], "anashid").Should().BeTrue();
        DatabaseSeeder.AppliesToTenant([], null).Should().BeTrue();
    }

    [Fact]
    public void AppliesToTenant_MatchingTenant_ShouldApply()
    {
        DatabaseSeeder.AppliesToTenant(["anashid"], "anashid").Should().BeTrue();
    }

    [Fact]
    public void AppliesToTenant_MatchingTenant_IsCaseInsensitive()
    {
        DatabaseSeeder.AppliesToTenant(["anashid"], "ANASHID").Should().BeTrue();
        DatabaseSeeder.AppliesToTenant(["Anashid"], "anashid").Should().BeTrue();
    }

    [Fact]
    public void AppliesToTenant_DifferentTenant_ShouldNotApply()
    {
        DatabaseSeeder.AppliesToTenant(["anashid"], "some-other-tenant").Should().BeFalse();
    }

    [Fact]
    public void AppliesToTenant_NoTenantContext_ShouldNotApplyToScopedEntry()
    {
        // A tenant-scoped entry has no meaning outside multi-tenant mode / with no resolved tenant.
        DatabaseSeeder.AppliesToTenant(["anashid"], null).Should().BeFalse();
    }

    [Fact]
    public void AppliesToTenant_MultipleTenantIds_MatchesAnyOfThem()
    {
        DatabaseSeeder.AppliesToTenant(["tenant-a", "tenant-b"], "tenant-b").Should().BeTrue();
        DatabaseSeeder.AppliesToTenant(["tenant-a", "tenant-b"], "tenant-c").Should().BeFalse();
    }
}
