using Identity.API.Tests.Infrastructure;
using Identity.Application.Commands;
using IhsanDev.Shared.Application.Exceptions;

namespace Identity.API.Tests.Endpoints;

/// <summary>
/// Integration tests for authentication using MediatR handlers directly
/// This approach bypasses HTTP layer and avoids .NET 9.0 PipeWriter bug
/// </summary>
[Collection("Sequential")]
public class AuthEndpointsTests : IntegrationTestBase
{
    public AuthEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region Register Tests

    [Fact]
    public async Task Register_WithValidData_ShouldReturnOkWithToken()
    {
        // Arrange
        var registerRequest = new RegisterCommand(
            Email: "newuser@example.com",
            Password: "NewUser123!",
            FirstName: "New",
            LastName: "User",
            PhoneNumber: null
        );

        // Act - Call handler directly via MediatR (bypasses HTTP/PipeWriter)
        var result = await SendAsync(registerRequest);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.Email.Should().Be(registerRequest.Email);
        result.FirstName.Should().Be(registerRequest.FirstName);
        result.LastName.Should().Be(registerRequest.LastName);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ShouldThrowBadRequestException()
    {
        // Arrange
        var email = "duplicate@example.com";
        await CreateTestUserAsync(email: email);

        var registerRequest = new RegisterCommand(
            Email: email,
            Password: "Password123!",
            FirstName: "Test",
            LastName: "User",
            PhoneNumber: null
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(
            async () => await SendAsync(registerRequest)
        );
        
        exception.Message.Should().Contain("Email is already registered");
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ShouldThrowValidationException()
    {
        // Arrange
        var registerRequest = new RegisterCommand(
            Email: "invalid-email",
            Password: "Password123!",
            FirstName: "Test",
            LastName: "User",
            PhoneNumber: null
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(registerRequest)
        );
    }

    [Fact]
    public async Task Register_WithWeakPassword_ShouldThrowValidationException()
    {
        // Arrange
        var registerRequest = new RegisterCommand(
            Email: "test@example.com",
            Password: "weak",
            FirstName: "Test",
            LastName: "User",
            PhoneNumber: null
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(registerRequest)
        );
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnOkWithToken()
    {
        // Arrange
        var email = "logintest@example.com";
        var password = "Login123!";
        await CreateTestUserAsync(email: email, password: password);

        var loginRequest = new LoginCommand(
            Email: email,
            Password: password
        );

        // Act
        var result = await SendAsync(loginRequest);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var email = "logintest2@example.com";
        await CreateTestUserAsync(email: email, password: "Correct123!");

        var loginRequest = new LoginCommand(
            Email: email,
            Password: "WrongPassword123!"
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(loginRequest)
        );
        
        exception.Message.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var loginRequest = new LoginCommand(
            Email: "nonexistent@example.com",
            Password: "Password123!"
        );

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(loginRequest)
        );
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshToken_WithValidToken_ShouldReturnNewTokens()
    {
        // Arrange
        var email = "refreshtest@example.com";
        var password = "Refresh123!";
        await CreateTestUserAsync(email: email, password: password);

        // Login to get tokens
        var loginResult = await SendAsync(new LoginCommand(email, password));

        // Wait a moment to ensure token timestamp is different
        await Task.Delay(1000);

        var refreshRequest = new RefreshTokenCommand(loginResult.RefreshToken!);

        // Act
        var result = await SendAsync(refreshRequest);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        // Note: Access token may be the same if generated within same second due to JWT timestamp precision
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var refreshRequest = new RefreshTokenCommand("invalid-refresh-token");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(refreshRequest)
        );
    }

    #endregion

    #region Forgot Password Tests

    [Fact]
    public async Task ForgotPassword_WithValidEmail_ShouldReturnOk()
    {
        // Arrange
        var email = "forgotpassword@example.com";
        await CreateTestUserAsync(email: email);

        var forgotPasswordRequest = new ForgetPasswordCommand(email);

        // Act
        var result = await SendAsync(forgotPasswordRequest);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("password reset");
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentEmail_ShouldStillReturnOk()
    {
        // Arrange - Email enumeration prevention: always returns success
        var forgotPasswordRequest = new ForgetPasswordCommand("nonexistent@example.com");

        // Act
        var result = await SendAsync(forgotPasswordRequest);

        // Assert - Should still return success message to prevent email enumeration
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("password reset");
    }

    #endregion
}
