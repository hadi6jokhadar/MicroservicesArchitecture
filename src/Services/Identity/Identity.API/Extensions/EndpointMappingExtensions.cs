using Identity.API.Filters;
using Identity.API.Handlers;
using Identity.Application.Commands;
using Identity.Application.Commands.Auth;
using Identity.Application.Commands.DeviceToken;
using Identity.Application.Commands.Admin.Role;
using Identity.Application.Commands.Admin.Claim;
using Identity.Application.DTOs;
using IhsanDev.Shared.Infrastructure.Attributes;

namespace Identity.API.Extensions;

public static class EndpointMappingExtensions
{
    /// <summary>
    /// Map all user-related API endpoints
    /// </summary>
    public static WebApplication MapUserEndpoints(this WebApplication app)
    {
        var userGroup = app.MapGroup("/api/user")
            .WithTags("Profile Management")
            .RequireAuthorization(policy => policy.RequireRole("User", "Admin", "SuperAdmin"))
            .WithOpenApi()
            .WithMetadata(new OptionalTenantAttribute());

        // Profile endpoints - OptionalTenant: user can access their profile with or without tenant context
        userGroup.MapGet("/profile", UserApiHandlers.GetProfileHandler)
            .WithName("GetProfile")
            .WithSummary("Get current user profile")
            .WithDescription("Get the profile information of the authenticated user. x-tenant-id header is optional.")
            .Produces<object>(200);

        userGroup.MapPut("/profile", UserApiHandlers.UpdateProfileHandler)
            .WithName("UpdateProfile")
            .WithSummary("Update current user profile")
            .WithDescription("Update the profile information of the authenticated user. x-tenant-id header is optional.")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<UpdateProfileCommand>>();

        userGroup.MapDelete("/me", UserApiHandlers.DeleteUserHandler)
            .WithName("DeleteUser")
            .WithSummary("Delete current user account")
            .WithDescription("Permanently delete the authenticated user's account. x-tenant-id header is optional.")
            .Produces<object>(200);

        return app;
    }

    /// <summary>
    /// Map auth-specific endpoints (alternative grouping)
    /// </summary>
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var authGroup = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .WithOpenApi()
            .WithMetadata(new OptionalTenantAttribute());

        authGroup.MapPost("/login", AuthApiHandlers.LoginHandler)
            .WithName("AuthLogin")
            .WithSummary("User login")
            .WithDescription("Authenticate user with email and password. x-tenant-id header is optional.")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<LoginCommand>>();

        authGroup.MapPost("/register", AuthApiHandlers.RegisterHandler)
            .WithName("AuthRegister")
            .WithSummary("Register a new user account")
            .WithDescription("Create a new user account with email and password. x-tenant-id header is optional.")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<RegisterCommand>>();

        authGroup.MapPost("/forgot-password", AuthApiHandlers.ForgotPasswordHandler)
            .WithName("ForgotPassword")
            .WithSummary("Request password reset")
            .WithDescription("Send password reset link to user's email. x-tenant-id header is optional.")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<ForgetPasswordCommand>>();

        authGroup.MapPost("/refresh", AuthApiHandlers.RefreshTokenHandler)
            .WithName("AuthRefresh")
            .WithSummary("Refresh access token")
            .WithDescription("Get a new access token using refresh token. x-tenant-id header is optional.")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<RefreshTokenCommand>>();

        authGroup.MapPost("/logout", AuthApiHandlers.LogoutHandler)
            .RequireAuthorization()
            .WithName("AuthLogout")
            .WithSummary("Logout current user")
            .WithDescription("Revoke refresh token and logout user. x-tenant-id header is optional.")
            .Produces<object>(200);

        // Phone verification endpoints
        authGroup.MapPost("/get-verification-code-by-phone", AuthApiHandlers.GetVerificationCodeByPhoneHandler)
            .WithName("GetVerificationCodeByPhone")
            .WithSummary("Get verification code for phone number")
            .WithDescription("Generate and send a 5-digit verification code to the user's phone number. x-tenant-id header is optional.")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<GetVerificationCodeByPhoneCommand>>();

        authGroup.MapPost("/get-verification-code-by-email", AuthApiHandlers.GetVerificationCodeByEmailHandler)
            .WithName("GetVerificationCodeByEmail")
            .WithSummary("Get verification code for email")
            .WithDescription("Generate and send a 5-digit verification code to the user's email. x-tenant-id header is optional.")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<GetVerificationCodeByEmailCommand>>();

        authGroup.MapPost("/login-with-code-by-phone", AuthApiHandlers.LoginWithCodeByPhoneHandler)
            .WithName("LoginWithCodeByPhone")
            .WithSummary("Login with phone number and verification code")
            .WithDescription("Authenticate user using phone number and verification code. x-tenant-id header is optional.")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<LoginWithCodeByPhoneCommand>>();

        authGroup.MapPost("/login-with-code-by-email", AuthApiHandlers.LoginWithCodeByEmailHandler)
            .WithName("LoginWithCodeByEmail")
            .WithSummary("Login with email and verification code")
            .WithDescription("Authenticate user using email and verification code. x-tenant-id header is optional.")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<LoginWithCodeByEmailCommand>>();

        authGroup.MapPost("/register-with-code-by-phone", AuthApiHandlers.RegisterWithCodeByPhoneHandler)
            .WithName("RegisterWithCodeByPhone")
            .WithSummary("Register new user with phone verification")
            .WithDescription("Create a new user account using phone number (no email or password required). x-tenant-id header is optional.")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<RegisterWithCodeByPhoneCommand>>();

        authGroup.MapPost("/register-with-code-by-email", AuthApiHandlers.RegisterWithCodeByEmailHandler)
            .WithName("RegisterWithCodeByEmail")
            .WithSummary("Register new user with email verification")
            .WithDescription("Create a new user account using email (no phone or password required). x-tenant-id header is optional.")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<RegisterWithCodeByEmailCommand>>();

        return app;
    }

    /// <summary>
    /// Map admin-related API endpoints
    /// </summary>
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        var adminGroup = app.MapGroup("/api/admin")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin")) // Require Admin role
            .WithTags("Admin User Management")
            .WithOpenApi()
            .WithMetadata(new OptionalTenantAttribute());

        // User management endpoints (Admin only) - OptionalTenant: Admin can manage users across tenants or global database
        adminGroup.MapGet("/users", AdminApiHandlers.GetUsersHandler)
            .WithName("GetAllUsers")
            .WithSummary("Get all users")
            .WithDescription("Retrieve paginated list of all users (Admin only). x-tenant-id header is optional - if provided, retrieves tenant users; otherwise retrieves global users.")
            .Produces<object>(200);

        adminGroup.MapGet("/users/{id:int}", AdminApiHandlers.GetUserByIdHandler)
            .WithName("GetUserById")
            .WithSummary("Get user by ID")
            .WithDescription("Retrieve user details by ID (Admin only). x-tenant-id header is optional.")
            .Produces<object>(200);

        adminGroup.MapPost("/users", AdminApiHandlers.CreateUserHandler)
            .WithName("CreateUser")
            .WithSummary("Create new user")
            .WithDescription("Create a new user account (Admin only). x-tenant-id header is optional - if provided, creates user in tenant database; otherwise in global database.")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<CreateUserCommand>>();

        adminGroup.MapPut("/users/{id:int}", AdminApiHandlers.UpdateUserHandler)
            .WithName("UpdateUser")
            .WithSummary("Update user")
            .WithDescription("Update user information (Admin only). x-tenant-id header is optional.")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<UpdateUserCommand>>();

        adminGroup.MapPatch("/users/{id:int}/toggle-status", AdminApiHandlers.ToggleUserStatusHandler)
            .WithName("ToggleUserStatus")
            .WithSummary("Toggle user status")
            .WithDescription("Enable or disable user account (Admin only). x-tenant-id header is optional.")
            .Produces<object>(200);

        adminGroup.MapDelete("/users/{id:int}", AdminApiHandlers.DeleteUserHandler)
            .WithName("DeleteUserById")
            .WithSummary("Delete user")
            .WithDescription("Permanently delete user account (Admin only). x-tenant-id header is optional.")
            .Produces<object>(200);

        return app;
    }

    /// <summary>
    /// Map role management endpoints
    /// </summary>
    public static WebApplication MapRoleEndpoints(this WebApplication app)
    {
        var roleGroup = app.MapGroup("/api/admin/roles")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"))
            .WithTags("Role Management")
            .WithOpenApi()
            .WithMetadata(new OptionalTenantAttribute());

        // Get all roles
        roleGroup.MapGet("/", RoleApiHandlers.GetRolesHandler)
            .WithName("GetAllRoles")
            .WithSummary("Get all roles")
            .WithDescription("Retrieve all roles in the system. x-tenant-id header is optional.")
            .Produces<List<RoleDto>>(200);

        // Get role by ID
        roleGroup.MapGet("/{id:int}", RoleApiHandlers.GetRoleByIdHandler)
            .WithName("GetRoleById")
            .WithSummary("Get role by ID")
            .WithDescription("Retrieve a specific role by ID. x-tenant-id header is optional.")
            .Produces<RoleDto>(200);

        // Create new role
        roleGroup.MapPost("/", RoleApiHandlers.CreateRoleHandler)
            .WithName("CreateRole")
            .WithSummary("Create new role")
            .WithDescription("Create a new role (Admin/SuperAdmin only). x-tenant-id header is optional.")
            .Produces<RoleDto>(201)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<CreateRoleCommand>>();

        // Update role
        roleGroup.MapPut("/{id:int}", RoleApiHandlers.UpdateRoleHandler)
            .WithName("UpdateRole")
            .WithSummary("Update role")
            .WithDescription("Update an existing role (Admin/SuperAdmin only). System roles cannot be renamed. x-tenant-id header is optional.")
            .Produces<RoleDto>(200)
            .ProducesValidationProblem();

        // Delete role
        roleGroup.MapDelete("/{id:int}", RoleApiHandlers.DeleteRoleHandler)
            .WithName("DeleteRole")
            .WithSummary("Delete role")
            .WithDescription("Delete a role (Admin/SuperAdmin only). System roles cannot be deleted. x-tenant-id header is optional.")
            .Produces<object>(200);

        // Assign claims to role
        roleGroup.MapPost("/{id:int}/claims", RoleApiHandlers.AssignClaimsToRoleHandler)
            .WithName("AssignClaimsToRole")
            .WithSummary("Assign claims to role")
            .WithDescription("Assign permissions (claims) to a role (Admin/SuperAdmin only). x-tenant-id header is optional.")
            .Produces<object>(200)
            .ProducesValidationProblem();

        // Assign roles to user (replaces existing roles)
        roleGroup.MapPost("/user/{id:int}", RoleApiHandlers.AssignRolesToUserHandler)
            .WithName("AssignRolesToUser")
            .WithSummary("Assign roles to user")
            .WithDescription("Assign roles to a user, replacing all existing roles (Admin/SuperAdmin only). x-tenant-id header is optional.")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<AssignRolesToUserCommand>>();

        return app;
    }

    /// <summary>
    /// Map claim management endpoints
    /// </summary>
    public static WebApplication MapClaimEndpoints(this WebApplication app)
    {
        var claimGroup = app.MapGroup("/api/admin/claims")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"))
            .WithTags("Claim Management")
            .WithOpenApi()
            .WithMetadata(new OptionalTenantAttribute());

        // Get all claims
        claimGroup.MapGet("/", ClaimApiHandlers.GetClaimsHandler)
            .WithName("GetAllClaims")
            .WithSummary("Get all claims")
            .WithDescription("Retrieve all available claims (permissions) in the system. x-tenant-id header is optional.")
            .Produces<List<ClaimDto>>(200);

        // Get claim by ID
        claimGroup.MapGet("/{id:int}", ClaimApiHandlers.GetClaimByIdHandler)
            .WithName("GetClaimById")
            .WithSummary("Get claim by ID")
            .WithDescription("Retrieve a specific claim by ID. x-tenant-id header is optional.")
            .Produces<ClaimDto>(200);

        // Create new claim
        claimGroup.MapPost("/", ClaimApiHandlers.CreateClaimHandler)
            .WithName("CreateClaim")
            .WithSummary("Create new claim")
            .WithDescription("Create a new claim/permission (Admin/SuperAdmin only). x-tenant-id header is optional.")
            .Produces<ClaimDto>(201)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<CreateClaimCommand>>();

        // Update claim
        claimGroup.MapPut("/{id:int}", ClaimApiHandlers.UpdateClaimHandler)
            .WithName("UpdateClaim")
            .WithSummary("Update claim")
            .WithDescription("Update an existing claim/permission (Admin/SuperAdmin only). x-tenant-id header is optional.")
            .Produces<ClaimDto>(200)
            .ProducesValidationProblem();

        // Delete claim
        claimGroup.MapDelete("/{id:int}", ClaimApiHandlers.DeleteClaimHandler)
            .WithName("DeleteClaim")
            .WithSummary("Delete claim")
            .WithDescription("Delete a claim/permission (Admin/SuperAdmin only). x-tenant-id header is optional.")
            .Produces<object>(200);

        return app;
    }

    /// <summary>
    /// Map device token management endpoints
    /// </summary>
    public static WebApplication MapDeviceTokenEndpoints(this WebApplication app)
    {
        var deviceTokenGroup = app.MapGroup("/api/device-tokens")
            .WithTags("Device Token Management")
            .RequireAuthorization(policy => policy.RequireRole("Service", "User", "Admin", "SuperAdmin"))
            .WithOpenApi()
            .WithMetadata(new OptionalTenantAttribute());

        // Add device token - OptionalTenant: Can register device tokens with or without tenant context
        deviceTokenGroup.MapPost("/", DeviceTokenApiHandlers.AddDeviceToken)
            .WithName("AddDeviceToken")
            .WithSummary("Add a new device token")
            .WithDescription("Register a new device token for push notifications. x-tenant-id header is optional.")
            .Produces<IhsanDev.Shared.Kernel.Dto.DeviceTokenDto>(201)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<AddDeviceTokenCommand>>();

        // Get device token by ID - OptionalTenant
        deviceTokenGroup.MapGet("/{id:int}", DeviceTokenApiHandlers.GetDeviceTokenById)
            .WithName("GetDeviceTokenById")
            .WithSummary("Get device token by ID")
            .WithDescription("Retrieve a specific device token by ID. x-tenant-id header is optional.")
            .Produces<IhsanDev.Shared.Kernel.Dto.DeviceTokenDto>(200)
            .Produces(404);

        // Get all device tokens for a user - OptionalTenant
        deviceTokenGroup.MapGet("/user/{userId:int}", DeviceTokenApiHandlers.GetUserDeviceTokens)
            .WithName("GetUserDeviceTokens")
            .WithSummary("Get all device tokens for a user")
            .WithDescription("Retrieve all device tokens registered for a specific user. x-tenant-id header is optional.")
            .Produces<List<IhsanDev.Shared.Kernel.Dto.DeviceTokenDto>>(200);

        // Get device tokens by user and platform - OptionalTenant
        deviceTokenGroup.MapGet("/user/{userId:int}/platform", DeviceTokenApiHandlers.GetUserDeviceTokensByPlatform)
            .WithName("GetUserDeviceTokensByPlatform")
            .WithSummary("Get device tokens by user and platform")
            .WithDescription("Retrieve device tokens for a specific user filtered by platform. x-tenant-id header is optional.")
            .Produces<List<IhsanDev.Shared.Kernel.Dto.DeviceTokenDto>>(200);

        // Update device token - OptionalTenant
        deviceTokenGroup.MapPut("/{id:int}", DeviceTokenApiHandlers.UpdateDeviceToken)
            .WithName("UpdateDeviceToken")
            .WithSummary("Update a device token")
            .WithDescription("Update an existing device token's information. x-tenant-id header is optional.")
            .Produces<IhsanDev.Shared.Kernel.Dto.DeviceTokenDto>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<UpdateDeviceTokenCommand>>();

        // Delete device token - OptionalTenant
        deviceTokenGroup.MapDelete("/{id:int}", DeviceTokenApiHandlers.DeleteDeviceToken)
            .WithName("DeleteDeviceToken")
            .WithSummary("Delete a device token")
            .WithDescription("Remove a specific device token. x-tenant-id header is optional.")
            .Produces(204)
            .Produces(404);

        // Delete all user device tokens - OptionalTenant
        deviceTokenGroup.MapDelete("/user/{userId:int}", DeviceTokenApiHandlers.DeleteAllUserDeviceTokens)
            .WithName("DeleteAllUserDeviceTokens")
            .WithSummary("Delete all device tokens for a user")
            .WithDescription("Remove all device tokens registered for a specific user. x-tenant-id header is optional.")
            .Produces(204);

        // Batch operations (service-to-service) - OptionalTenant for cross-tenant operations
        deviceTokenGroup.MapPost("/batch", DeviceTokenApiHandlers.GetBatchDeviceTokens)
            .WithName("GetBatchDeviceTokens")
            .WithSummary("Get device tokens for multiple users (batch)")
            .WithDescription("Retrieve device tokens for multiple users in a single request. For service-to-service communication. x-tenant-id header is optional.")
            .Produces<Dictionary<int, List<IhsanDev.Shared.Kernel.Dto.DeviceTokenDto>>>(200);

        deviceTokenGroup.MapDelete("/batch", DeviceTokenApiHandlers.DeleteBatchDeviceTokens)
            .WithName("DeleteBatchDeviceTokens")
            .WithSummary("Delete multiple device tokens (batch)")
            .WithDescription("Delete multiple device tokens in a single request. For service-to-service communication. x-tenant-id header is optional.")
            .Produces<object>(200);

        // Get all device tokens for current tenant - OptionalTenant (works as global if no tenant context)
        deviceTokenGroup.MapGet("/tenant", DeviceTokenApiHandlers.GetTenantDeviceTokens)
            .WithName("GetTenantDeviceTokens")
            .WithSummary("Get all device tokens for current tenant or global")
            .WithDescription("Retrieve all device tokens for the current tenant if x-tenant-id is provided, otherwise retrieves from global database.")
            .Produces<List<IhsanDev.Shared.Kernel.Dto.DeviceTokenDto>>(200);

        return app;
    }
}