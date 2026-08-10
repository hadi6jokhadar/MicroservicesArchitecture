using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Application.Services;
using Identity.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace Identity.Infrastructure.Services;

public class DatabaseSeeder
{
    private readonly IRoleRepository _roleRepository;
    private readonly IClaimRepository _claimRepository;
    private readonly IRoleClaimRepository _roleClaimRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        IRoleRepository roleRepository,
        IClaimRepository claimRepository,
        IRoleClaimRepository roleClaimRepository,
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IPasswordHasher passwordHasher,
        ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<DatabaseSeeder> logger)
    {
        _roleRepository = roleRepository;
        _claimRepository = claimRepository;
        _roleClaimRepository = roleClaimRepository;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedDefaultRolesAndClaimsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting database seeding for roles, claims, and SuperAdmin user...");

        // 1. Seed default roles (SuperAdmin, Admin, User)
        await SeedDefaultRolesAsync(cancellationToken);

        // 2. Seed default claims
        await SeedDefaultClaimsAsync(cancellationToken);

        // 3. Assign default claims to roles
        await AssignDefaultClaimsToRolesAsync(cancellationToken);

        // 4. Create SuperAdmin user if not exists
        await CreateSuperAdminUserAsync(cancellationToken);

        _logger.LogInformation("Database seeding completed successfully.");
    }

    private bool AppliesToCurrentTenant(string[]? tenantIds) =>
        AppliesToTenant(tenantIds, _tenantContext.CurrentTenant?.TenantId);

    /// <summary>
    /// True for platform-wide catalog entries (<c>tenantIds</c> null/empty) or when
    /// <paramref name="currentTenantId"/> is explicitly listed (case-insensitive) — false
    /// otherwise, and false when <paramref name="currentTenantId"/> is null (a tenant-scoped
    /// entry has no meaning outside multi-tenant mode). Pure/static so it's testable without a
    /// database or DI container — see <c>DatabaseSeederTenantScopingTests</c>.
    /// </summary>
    public static bool AppliesToTenant(string[]? tenantIds, string? currentTenantId)
    {
        if (tenantIds == null || tenantIds.Length == 0)
        {
            return true;
        }

        return currentTenantId != null &&
            tenantIds.Any(t => string.Equals(t, currentTenantId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SeedDefaultRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleDef in SystemPermissionCatalog.AllRoles)
        {
            if (!AppliesToCurrentTenant(roleDef.TenantIds))
            {
                continue;
            }

            var existingRole = await _roleRepository.GetByNameAsync(roleDef.Name, cancellationToken);
            if (existingRole == null)
            {
                var role = new Role
                {
                    Name = roleDef.Name,
                    NormalizedName = roleDef.Name.ToUpperInvariant(),
                    Description = roleDef.Description,
                    IsSystemRole = true, // System roles cannot be deleted or renamed
                    Status = true
                };

                await _roleRepository.CreateAsync(role, cancellationToken);
                _logger.LogInformation("Created system role: {RoleName}", roleDef.Name);
            }
            else
            {
                _logger.LogDebug("System role already exists: {RoleName}", roleDef.Name);
            }
        }
    }

    private async Task SeedDefaultClaimsAsync(CancellationToken cancellationToken)
    {
        foreach (var claimDef in SystemPermissionCatalog.AllClaims)
        {
            if (!AppliesToCurrentTenant(claimDef.TenantIds))
            {
                continue;
            }

            var existingClaim = await _claimRepository.GetByClaimValueAsync(claimDef.ClaimValue, cancellationToken);
            if (existingClaim == null)
            {
                var claim = new Claim
                {
                    Name = claimDef.Name,
                    NormalizedName = claimDef.Name.ToUpperInvariant(),
                    ClaimType = claimDef.ClaimType,
                    ClaimValue = claimDef.ClaimValue,
                    Description = claimDef.Description,
                    IsSuperAdminOnly = claimDef.IsSuperAdminOnly,
                    IsSystemClaim = true, // System claims cannot be deleted or renamed
                    Status = true
                };

                await _claimRepository.CreateAsync(claim, cancellationToken);
                _logger.LogInformation("Created system claim: {ClaimValue}", claimDef.ClaimValue);
            }
            else
            {
                _logger.LogDebug("System claim already exists: {ClaimValue}", claimDef.ClaimValue);
            }
        }
    }

    private async Task AssignDefaultClaimsToRolesAsync(CancellationToken cancellationToken)
    {
        // Additive only — never revokes a claim, so an admin's own manual extra assignments on a
        // catalog role (or removal of one of these) survive across restarts/reseeds.
        foreach (var roleDef in SystemPermissionCatalog.AllRoles)
        {
            if (roleDef.ClaimValues.Length == 0 || !AppliesToCurrentTenant(roleDef.TenantIds))
            {
                continue;
            }

            var role = await _roleRepository.GetByNameAsync(roleDef.Name, cancellationToken);
            if (role == null)
            {
                _logger.LogWarning("Role '{RoleName}' not found, skipping its claim assignments", roleDef.Name);
                continue;
            }

            foreach (var claimValue in roleDef.ClaimValues)
            {
                var claim = await _claimRepository.GetByClaimValueAsync(claimValue, cancellationToken);
                if (claim == null)
                {
                    _logger.LogWarning("Claim '{ClaimValue}' not found, skipping assignment to role '{RoleName}'", claimValue, roleDef.Name);
                    continue;
                }

                var hasClaimAlready = await _roleClaimRepository.RoleHasClaimAsync(role.Id, claim.Id, cancellationToken);
                if (!hasClaimAlready)
                {
                    await _roleClaimRepository.AssignClaimsToRoleAsync(role.Id, [claim.Id], cancellationToken);
                    _logger.LogInformation("Assigned '{ClaimValue}' claim to {RoleName} role", claimValue, roleDef.Name);
                }
            }
        }
    }

    private async Task CreateSuperAdminUserAsync(CancellationToken cancellationToken)
    {
        // Determine SuperAdmin email based on tenant context
        string superAdminEmail;
        if (_tenantContext.IsMultiTenantMode && _tenantContext.HasTenant)
        {
            // For tenant databases: {tenantId}@ihsandev.com
            var tenantId = _tenantContext.CurrentTenant?.TenantId ?? "default";
            superAdminEmail = $"{tenantId}@ihsandev.com";
            _logger.LogDebug("Creating SuperAdmin for tenant '{TenantId}' with email '{Email}'", tenantId, superAdminEmail);
        }
        else
        {
            // For global database: superadmin@ihsandev.com
            superAdminEmail = "superadmin@ihsandev.com";
            _logger.LogDebug("Creating SuperAdmin for global database with email '{Email}'", superAdminEmail);
        }

        // Check if SuperAdmin user already exists
        var existingUser = await _userRepository.GetByEmailAsync(superAdminEmail, cancellationToken);
        if (existingUser != null)
        {
            _logger.LogDebug("SuperAdmin user already exists: {Email}", superAdminEmail);

            // Ensure SuperAdmin has the SuperAdmin role assigned
            await EnsureSuperAdminRoleAsync(existingUser.Id, cancellationToken);
            return;
        }

        // Never ship a literal password constant — every deployment (and every tenant DB)
        // used to get the exact same hardcoded "@Test123" SuperAdmin password, giving anyone
        // day-one SuperAdmin on any environment that hadn't been manually rotated. The password
        // now comes from SeedData:SuperAdminPassword instead: the tracked appsettings.json only
        // carries a CHANGE_ME_* placeholder (this repo's established secrets pattern — see
        // Dotnet.instructions.md pitfall #16), and the real value is set per-environment in the
        // gitignored appsettings.Development.json / appsettings.Docker.json. Fail fast rather
        // than silently seeding a known-weak account if that hasn't been done.
        var superAdminPassword = _configuration["SeedData:SuperAdminPassword"];
        if (string.IsNullOrWhiteSpace(superAdminPassword) ||
            string.Equals(superAdminPassword, "CHANGE_ME_SUPERADMIN_PASSWORD", StringComparison.OrdinalIgnoreCase) ||
            superAdminPassword.Length < 8)
        {
            throw new InvalidOperationException(
                "SeedData:SuperAdminPassword is not configured (or is still the CHANGE_ME_* placeholder / shorter " +
                "than 8 characters). Set a real password in appsettings.Development.json / appsettings.Docker.json " +
                "before the SuperAdmin account can be seeded.");
        }

        // Create SuperAdmin user
        var superAdminUser = new User
        {
            Email = superAdminEmail,
            FirstName = "Super",
            LastName = "Admin",
            PasswordHash = _passwordHasher.HashPassword(superAdminPassword),
            EmailConfirmed = true,
            Status = true,
            IsArchived = false
        };

        await _userRepository.AddAsync(superAdminUser, cancellationToken);
        _logger.LogInformation("Created SuperAdmin user: {Email}", superAdminEmail);

        // Assign SuperAdmin role
        await EnsureSuperAdminRoleAsync(superAdminUser.Id, cancellationToken);
    }

    private async Task EnsureSuperAdminRoleAsync(int userId, CancellationToken cancellationToken)
    {
        // Get SuperAdmin role
        var superAdminRole = await _roleRepository.GetByNameAsync("SuperAdmin", cancellationToken);
        if (superAdminRole == null)
        {
            _logger.LogWarning("SuperAdmin role not found, cannot assign to user {UserId}", userId);
            return;
        }

        // Check if user already has SuperAdmin role
        var hasRole = await _userRoleRepository.UserHasRoleAsync(userId, "SuperAdmin", cancellationToken);
        if (hasRole)
        {
            _logger.LogDebug("User {UserId} already has SuperAdmin role", userId);
            return;
        }

        // Assign SuperAdmin role to user
        await _userRoleRepository.AssignRolesToUserAsync(userId, [superAdminRole.Id], cancellationToken);
        _logger.LogInformation("Assigned SuperAdmin role to user {UserId}", userId);
    }
}
