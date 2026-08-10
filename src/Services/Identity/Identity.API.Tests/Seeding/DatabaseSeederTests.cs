using Identity.API.Tests.Infrastructure;
using Identity.Domain.Entities;
using Identity.Infrastructure.Seeding;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Tests.Seeding;

/// <summary>
/// Integration tests for <see cref="DatabaseSeeder"/> — the catalog-driven seeder that creates
/// system roles/claims from <see cref="SystemPermissionCatalog"/> on every tenant's first request.
/// Runs against the same real Postgres test DB as every other Identity integration test; the test
/// harness runs with MultiTenancy disabled (see CustomWebApplicationFactory), so every tenant-scoped
/// catalog entry (e.g. Nasheed's) is expected to be skipped — see also
/// <see cref="DatabaseSeederTenantScopingTests"/> for the pure tenant-matching logic.
/// </summary>
[Collection("Sequential")]
public class DatabaseSeederTests : IntegrationTestBase
{
    public DatabaseSeederTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    private Task RunSeederAsync() =>
        ResolveScopedAsync<DatabaseSeeder>(seeder => seeder.SeedDefaultRolesAndClaimsAsync());

    [Fact]
    public async Task SeedDefaultRolesAndClaimsAsync_ShouldEnsureBaseSystemRolesExist()
    {
        // Act
        await RunSeederAsync();

        // Assert
        var roles = await ExecuteDbContextAsync(async context =>
            await context.Roles
                .Where(r => r.Name == "User" || r.Name == "Admin" || r.Name == "SuperAdmin")
                .ToListAsync());

        roles.Should().HaveCount(3);
        roles.Should().OnlyContain(r => r.IsSystemRole);
    }

    [Fact]
    public async Task SeedDefaultRolesAndClaimsAsync_ShouldCreatePlatformWideDeleteClaimAsSystemClaim()
    {
        // Act
        await RunSeederAsync();

        // Assert
        var deleteClaim = await ExecuteDbContextAsync(async context =>
            await context.Claims.FirstOrDefaultAsync(c => c.ClaimValue == "actions.delete"));

        deleteClaim.Should().NotBeNull();
        deleteClaim!.ClaimType.Should().Be("Permission");
        deleteClaim.IsSystemClaim.Should().BeTrue();
    }

    [Fact]
    public async Task SeedDefaultRolesAndClaimsAsync_ShouldAssignDeleteClaimToAdminAndSuperAdmin()
    {
        // Act
        await RunSeederAsync();

        // Assert
        var assignments = await ExecuteDbContextAsync(async context =>
        {
            var deleteClaim = await context.Claims.FirstAsync(c => c.ClaimValue == "actions.delete");
            return await context.RoleClaims
                .Include(rc => rc.Role)
                .Where(rc => rc.ClaimId == deleteClaim.Id)
                .ToListAsync();
        });

        assignments.Should().Contain(a => a.Role.Name == "Admin");
        assignments.Should().Contain(a => a.Role.Name == "SuperAdmin");
        assignments.Should().NotContain(a => a.Role.Name == "User");
    }

    [Fact]
    public async Task SeedDefaultRolesAndClaimsAsync_CalledTwice_ShouldNotDuplicateRolesOrClaimsOrAssignments()
    {
        // Act — run twice in a row, exactly what happens across repeated requests/restarts
        await RunSeederAsync();
        await RunSeederAsync();

        // Assert — no duplicate role rows
        var roleNameCounts = await ExecuteDbContextAsync(async context =>
            await context.Roles
                .Where(r => r.Name == "Admin" || r.Name == "SuperAdmin" || r.Name == "User")
                .GroupBy(r => r.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync());
        roleNameCounts.Should().OnlyContain(g => g.Count == 1);

        // Assert — no duplicate claim rows
        var claimValueCounts = await ExecuteDbContextAsync(async context =>
            await context.Claims
                .Where(c => c.ClaimValue == "actions.delete")
                .GroupBy(c => c.ClaimValue)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToListAsync());
        claimValueCounts.Should().OnlyContain(g => g.Count == 1);

        // Assert — no duplicate role-claim assignment rows
        var assignmentCount = await ExecuteDbContextAsync(async context =>
        {
            var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
            var deleteClaim = await context.Claims.FirstAsync(c => c.ClaimValue == "actions.delete");
            return await context.RoleClaims
                .Where(rc => rc.RoleId == adminRole.Id && rc.ClaimId == deleteClaim.Id)
                .CountAsync();
        });
        assignmentCount.Should().Be(1);
    }

    [Fact]
    public async Task SeedDefaultRolesAndClaimsAsync_WithoutMatchingTenant_ShouldNotSeedNasheedTenantScopedEntries()
    {
        // Arrange — the test harness runs with MultiTenancy disabled (no tenant context at all),
        // so every entry scoped to a specific tenant (e.g. Nasheed's "anashid"-only claims/role)
        // must be skipped entirely — never created for the wrong (or no) tenant.

        // Act
        await RunSeederAsync();

        // Assert
        var nasheedRole = await ExecuteDbContextAsync(async context =>
            await context.Roles.FirstOrDefaultAsync(r => r.Name == "NasheedDataEntry"));
        nasheedRole.Should().BeNull();

        var nasheedClaims = await ExecuteDbContextAsync(async context =>
            await context.Claims.Where(c => c.ClaimValue.StartsWith("nasheed.")).ToListAsync());
        nasheedClaims.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedDefaultRolesAndClaimsAsync_ShouldPreserveManuallyAssignedExtraClaimOnReseed()
    {
        // Arrange — seed once, then simulate an admin manually granting Admin an extra claim
        // beyond the catalog (directly via EF, mirroring what IRoleClaimRepository.AssignClaimsToRoleAsync
        // does at the row level — AssignClaimsToRoleCommand itself is full-replace and would wipe
        // Admin's "actions.delete" claim too, so it can't be used here).
        await RunSeederAsync();

        var testId = Guid.NewGuid().ToString("N")[..8];
        var extraClaim = await ExecuteDbContextAsync(async context =>
        {
            var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
            var claim = new Claim
            {
                Name = $"ManualExtra_{testId}",
                NormalizedName = $"MANUALEXTRA_{testId}",
                ClaimType = "Permission",
                ClaimValue = $"manual.extra.{testId}",
                IsSystemClaim = false,
                Status = true
            };
            context.Claims.Add(claim);
            await context.SaveChangesAsync();

            context.RoleClaims.Add(new RoleClaim { RoleId = adminRole.Id, ClaimId = claim.Id });
            await context.SaveChangesAsync();

            return claim;
        });

        // Act — reseed
        await RunSeederAsync();

        // Assert — the manual extra assignment survives, and Admin still has the catalog's own claim
        var adminAssignments = await ExecuteDbContextAsync(async context =>
        {
            var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
            return await context.RoleClaims
                .Include(rc => rc.Claim)
                .Where(rc => rc.RoleId == adminRole.Id)
                .Select(rc => rc.Claim.ClaimValue)
                .ToListAsync();
        });

        adminAssignments.Should().Contain(extraClaim.ClaimValue);
        adminAssignments.Should().Contain("actions.delete");
    }

    [Fact]
    public async Task SeedDefaultRolesAndClaimsAsync_ShouldCreateSuperAdminUser()
    {
        // Act — test harness runs with MultiTenancy disabled, so the global-database email applies
        await RunSeederAsync();

        // Assert
        var superAdmin = await ExecuteDbContextAsync(async context =>
            await context.Users.FirstOrDefaultAsync(u => u.Email == "superadmin@ihsandev.com"));

        superAdmin.Should().NotBeNull();

        var hasRole = await ExecuteDbContextAsync(async context =>
        {
            var superAdminRole = await context.Roles.FirstAsync(r => r.Name == "SuperAdmin");
            return await context.UserRoles.AnyAsync(ur => ur.UserId == superAdmin!.Id && ur.RoleId == superAdminRole.Id);
        });
        hasRole.Should().BeTrue();
    }
}
