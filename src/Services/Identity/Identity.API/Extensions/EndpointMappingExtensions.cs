using Identity.API.Filters;
using Identity.API.Handlers;
using Identity.Application.Commands;
using Identity.Application.Commands.Auth;
using Identity.Application.DTOs;

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
            .RequireAuthorization(policy => policy.RequireRole("User"))
            .WithOpenApi();

        // Profile endpoints
        userGroup.MapGet("/profile", UserApiHandlers.GetProfileHandler)
            .WithName("GetProfile")
            .WithSummary("Get current user profile")
            .WithDescription("Get the profile information of the authenticated user")
            .Produces<object>(200);

        userGroup.MapPut("/profile", UserApiHandlers.UpdateProfileHandler)
            .WithName("UpdateProfile")
            .WithSummary("Update current user profile")
            .WithDescription("Update the profile information of the authenticated user")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<UpdateProfileCommand>>();

        userGroup.MapDelete("/me", UserApiHandlers.DeleteUserHandler)
            .WithName("DeleteUser")
            .WithSummary("Delete current user account")
            .WithDescription("Permanently delete the authenticated user's account")
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
            .WithOpenApi();

        authGroup.MapPost("/login", AuthApiHandlers.LoginHandler)
            .WithName("AuthLogin")
            .WithSummary("User login")
            .WithDescription("Authenticate user with email and password")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<LoginCommand>>();

        authGroup.MapPost("/register", AuthApiHandlers.RegisterHandler)
            .WithName("AuthRegister")
            .WithSummary("Register a new user account")
            .WithDescription("Create a new user account with email and password")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<RegisterCommand>>();

        authGroup.MapPost("/forgot-password", AuthApiHandlers.ForgotPasswordHandler)
            .WithName("ForgotPassword")
            .WithSummary("Request password reset")
            .WithDescription("Send password reset link to user's email")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<ForgetPasswordCommand>>();

        authGroup.MapPost("/refresh", AuthApiHandlers.RefreshTokenHandler)
            .WithName("AuthRefresh")
            .WithSummary("Refresh access token")
            .WithDescription("Get a new access token using refresh token")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<RefreshTokenCommand>>();

        authGroup.MapPost("/logout", AuthApiHandlers.LogoutHandler)
            .RequireAuthorization()
            .WithName("AuthLogout")
            .WithSummary("Logout current user")
            .WithDescription("Revoke refresh token and logout user")
            .Produces<object>(200);

        // Phone verification endpoints
        authGroup.MapPost("/get-verification-code-by-phone", AuthApiHandlers.GetVerificationCodeByPhoneHandler)
            .WithName("GetVerificationCodeByPhone")
            .WithSummary("Get verification code for phone number")
            .WithDescription("Generate and send a 5-digit verification code to the user's phone number")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<GetVerificationCodeByPhoneCommand>>();

        authGroup.MapPost("/get-verification-code-by-email", AuthApiHandlers.GetVerificationCodeByEmailHandler)
            .WithName("GetVerificationCodeByEmail")
            .WithSummary("Get verification code for email")
            .WithDescription("Generate and send a 5-digit verification code to the user's email")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<GetVerificationCodeByEmailCommand>>();

        authGroup.MapPost("/login-with-code-by-phone", AuthApiHandlers.LoginWithCodeByPhoneHandler)
            .WithName("LoginWithCodeByPhone")
            .WithSummary("Login with phone number and verification code")
            .WithDescription("Authenticate user using phone number and verification code")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<LoginWithCodeByPhoneCommand>>();

        authGroup.MapPost("/login-with-code-by-email", AuthApiHandlers.LoginWithCodeByEmailHandler)
            .WithName("LoginWithCodeByEmail")
            .WithSummary("Login with email and verification code")
            .WithDescription("Authenticate user using email and verification code")
            .Produces<UserDtoIncludesToken>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<LoginWithCodeByEmailCommand>>();

        authGroup.MapPost("/register-with-code-by-phone", AuthApiHandlers.RegisterWithCodeByPhoneHandler)
            .WithName("RegisterWithCodeByPhone")
            .WithSummary("Register new user with phone verification")
            .WithDescription("Create a new user account using phone number (no email or password required)")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<RegisterWithCodeByPhoneCommand>>();

        authGroup.MapPost("/register-with-code-by-email", AuthApiHandlers.RegisterWithCodeByEmailHandler)
            .WithName("RegisterWithCodeByEmail")
            .WithSummary("Register new user with email verification")
            .WithDescription("Create a new user account using email (no phone or password required)")
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
            .WithOpenApi();

        // User management endpoints (Admin only)
        adminGroup.MapGet("/users", AdminApiHandlers.GetUsersHandler)
            .WithName("GetAllUsers")
            .WithSummary("Get all users")
            .WithDescription("Retrieve paginated list of all users (Admin only)")
            .Produces<object>(200);

        adminGroup.MapGet("/users/{id:int}", AdminApiHandlers.GetUserByIdHandler)
            .WithName("GetUserById")
            .WithSummary("Get user by ID")
            .WithDescription("Retrieve user details by ID (Admin only)")
            .Produces<object>(200);

        adminGroup.MapPost("/users", AdminApiHandlers.CreateUserHandler)
            .WithName("CreateUser")
            .WithSummary("Create new user")
            .WithDescription("Create a new user account (Admin only)")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<CreateUserCommand>>();

        adminGroup.MapPut("/users/{id:int}", AdminApiHandlers.UpdateUserHandler)
            .WithName("UpdateUser")
            .WithSummary("Update user")
            .WithDescription("Update user information (Admin only)")
            .Produces<object>(200)
            .ProducesValidationProblem()
            .AddEndpointFilter<ValidationFilter<UpdateUserCommand>>();

        adminGroup.MapPatch("/users/{id:int}/toggle-status", AdminApiHandlers.ToggleUserStatusHandler)
            .WithName("ToggleUserStatus")
            .WithSummary("Toggle user status")
            .WithDescription("Enable or disable user account (Admin only)")
            .Produces<object>(200);

        adminGroup.MapDelete("/users/{id:int}", AdminApiHandlers.DeleteUserHandler)
            .WithName("DeleteUserById")
            .WithSummary("Delete user")
            .WithDescription("Permanently delete user account (Admin only)")
            .Produces<object>(200);

        return app;
    }
}