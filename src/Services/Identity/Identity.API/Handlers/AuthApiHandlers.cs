using System.Security.Claims;
using Identity.Application.Commands;
using Identity.Application.Commands.Auth;
using IhsanDev.Shared.Application.Localization;
using MediatR;

namespace Identity.API.Handlers;

public static class AuthApiHandlers
{
    /// <summary>
    /// Handle user login
    /// </summary>
    public static async Task<IResult> LoginHandler(
        LoginCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle user registration
    /// </summary>
    public static async Task<IResult> RegisterHandler(
        RegisterCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle get verification code by phone request
    /// </summary>
    public static async Task<IResult> GetVerificationCodeByPhoneHandler(
        GetVerificationCodeByPhoneCommand command,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(new { success = result, message = localizationService.GetString(LocalizationKeys.Success.VerificationCodeSentPhone) });
    }

    /// <summary>
    /// Handle get verification code by email request
    /// </summary>
    public static async Task<IResult> GetVerificationCodeByEmailHandler(
        GetVerificationCodeByEmailCommand command,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(new { success = result, message = localizationService.GetString(LocalizationKeys.Success.VerificationCodeSentEmail) });
    }

    /// <summary>
    /// Handle login with verification code by phone
    /// </summary>
    public static async Task<IResult> LoginWithCodeByPhoneHandler(
        LoginWithCodeByPhoneCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle login with verification code by email
    /// </summary>
    public static async Task<IResult> LoginWithCodeByEmailHandler(
        LoginWithCodeByEmailCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle registration with verification code by phone
    /// </summary>
    public static async Task<IResult> RegisterWithCodeByPhoneHandler(
        RegisterWithCodeByPhoneCommand command,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(new { success = result, message = localizationService.GetString(LocalizationKeys.Success.RegistrationSuccessfulLoginPhone) });
    }

    /// <summary>
    /// Handle registration with verification code by email
    /// </summary>
    public static async Task<IResult> RegisterWithCodeByEmailHandler(
        RegisterWithCodeByEmailCommand command,
        IMediator mediator,
        ILocalizationService localizationService,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(new { success = result, message = localizationService.GetString(LocalizationKeys.Success.RegistrationSuccessfulLoginEmail) });
    }

    /// <summary>
    /// Handle token refresh
    /// </summary>
    public static async Task<IResult> RefreshTokenHandler(
        RefreshTokenCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handle user logout
    /// </summary>
    public static IResult LogoutHandler(HttpContext context, ILocalizationService localizationService)
    {
        var userId = GetCurrentUserId(context);
        if (userId == 0)
        {
            return Results.Unauthorized();
        }

        // In a real application, you might want to blacklist the token
        // For now, we'll just return success as the token will expire naturally
        return Results.Ok(new { message = localizationService.GetString(LocalizationKeys.Success.LogoutSuccessful) });
    }

    /// <summary>
    /// Handle forgot password request
    /// </summary>
    public static async Task<IResult> ForgotPasswordHandler(
        ForgetPasswordCommand command,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Helper method to extract user ID from claims
    /// </summary>
    private static int GetCurrentUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }
}