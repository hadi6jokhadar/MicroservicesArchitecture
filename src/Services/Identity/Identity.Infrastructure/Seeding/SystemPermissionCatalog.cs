namespace Identity.Infrastructure.Seeding;

/// <summary>
/// A "Permission" claim seeded automatically by <see cref="DatabaseSeeder"/>. <see cref="ClaimValue"/>
/// is a literal string referenced by another service's authorization policy code (e.g. Nasheed.API's
/// Program.cs) — it is a wire contract, not just admin-UI data.
/// </summary>
public sealed record SystemClaimDefinition
{
    public required string ClaimValue { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsSuperAdminOnly { get; init; }

    /// <summary>
    /// Restricts seeding to specific tenants (case-insensitive tenant IDs). Null/empty = seed into
    /// every tenant. Use this for an app that only one tenant actually runs — e.g. Nasheed's claims
    /// only make sense for the "anashid" tenant; seeding them into every other tenant's Identity
    /// database would be noise nobody there can use.
    /// </summary>
    public string[]? TenantIds { get; init; }

    public string ClaimType => "Permission";
}

/// <summary>
/// A role seeded automatically, with the given claims attached. The role Name itself is never
/// checked by authorization code (only its claims are) — it exists purely so an admin can find and
/// assign a ready-made bundle of permissions to a user. Same tenant-scoping as <see cref="SystemClaimDefinition"/>.
/// </summary>
public sealed record SystemRoleDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string[] ClaimValues { get; init; } = [];
    public string[]? TenantIds { get; init; }
}

/// <summary>
/// Declarative, append-only registry of every role/claim the platform seeds on startup (via
/// <see cref="DatabaseSeeder"/>). Identity is the single owner of all Role/Claim data platform-wide —
/// no other app stores its own roles/claims, regardless of that app's own database strategy — so a
/// new app's permissions are added here once, not re-implemented as a new seeder per app.
///
/// Adding a new app's permissions: append its claims to <see cref="AllClaims"/> and any bundled role
/// to <see cref="AllRoles"/> below, setting <c>TenantIds</c> if the app is only deployed for specific
/// tenant(s). No other code changes are needed — <see cref="DatabaseSeeder"/> consumes this catalog
/// generically, filters by the current tenant, and is idempotent (skips anything already present)
/// and additive-only (never revokes a claim an admin manually assigned beyond this catalog).
/// </summary>
public static class SystemPermissionCatalog
{
    // ── Platform-wide claims (not owned by a specific app; seeded into every tenant) ────────
    private static readonly SystemClaimDefinition[] PlatformClaims =
    [
        new() { ClaimValue = "actions.delete", Name = "Delete Actions", Description = "Permission to perform delete actions" },
    ];

    // ── Nasheed (src/Apps/Nasheed) — deployed only for the "anashid" tenant ─────────────────
    // Grants create/edit for songs and create for artists, deliberately with no delete/artist-edit
    // claim — a role holding only these can never reach Nasheed's AdminOnly-gated delete/artist-edit
    // endpoints. See src/Apps/Nasheed/Doc/API_ENDPOINTS.md "Content-Editor Permission Claims".
    private static readonly string[] NasheedTenantIds = ["anashid"];

    private static readonly SystemClaimDefinition[] NasheedClaims =
    [
        new() { ClaimValue = "nasheed.pages.songs", Name = "View Songs Page", Description = "Frontend-only: shows the Nasheed admin Songs page/sidebar item", TenantIds = NasheedTenantIds },
        new() { ClaimValue = "nasheed.songs.create", Name = "Create Songs", Description = "Allows POST /api/songs", TenantIds = NasheedTenantIds },
        new() { ClaimValue = "nasheed.songs.edit", Name = "Edit Own Songs", Description = "Allows PUT /api/songs/{id}, restricted to songs the caller created", TenantIds = NasheedTenantIds },
        new() { ClaimValue = "nasheed.pages.artists", Name = "View Artists Page", Description = "Frontend-only: shows the Nasheed admin Artists page/sidebar item", TenantIds = NasheedTenantIds },
        new() { ClaimValue = "nasheed.artists.create", Name = "Create Artists", Description = "Allows POST /api/artists", TenantIds = NasheedTenantIds },
    ];

    /// <summary>Every claim the seeder considers — <see cref="DatabaseSeeder"/> filters by tenant.</summary>
    public static readonly SystemClaimDefinition[] AllClaims =
    [
        .. PlatformClaims,
        .. NasheedClaims,
    ];

    /// <summary>Every role the seeder considers, across all apps — includes the three base system roles (all tenants).</summary>
    public static readonly SystemRoleDefinition[] AllRoles =
    [
        new() { Name = "User", Description = "Default user role with basic permissions" },
        new() { Name = "Admin", Description = "Administrator role with management permissions", ClaimValues = ["actions.delete"] },
        new() { Name = "SuperAdmin", Description = "Super administrator role with full system access", ClaimValues = ["actions.delete"] },

        new()
        {
            Name = "NasheedDataEntry",
            Description = "Can add/edit their own Nasheed songs and add artists — cannot delete or manage other content",
            ClaimValues = ["nasheed.pages.songs", "nasheed.songs.create", "nasheed.songs.edit", "nasheed.pages.artists", "nasheed.artists.create"],
            TenantIds = NasheedTenantIds,
        },
    ];
}
