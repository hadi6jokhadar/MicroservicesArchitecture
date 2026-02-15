using Identity.API.Tests.Infrastructure;
using Identity.Application.Commands.Auth;
using Identity.Domain.Entities;
using IhsanDev.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Tests.Endpoints;

/// <summary>
/// Integration tests for OTP-based authentication (phone and email verification codes)
/// Tests all 6 OTP endpoints with security features (expiration, attempts, lockout, cooldown)
/// </summary>
[Collection("Sequential")]
public class OtpAuthEndpointsTests : IntegrationTestBase
{
    public OtpAuthEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region Get Verification Code By Phone Tests

    [Fact]
    public async Task GetVerificationCodeByPhone_WithValidPhoneNumber_ShouldReturnTrue()
    {
        // Arrange
        var phoneNumber = "+1234567890";
        await CreateTestUserWithPhoneAsync(phoneNumber);

        var request = new GetVerificationCodeByPhoneCommand(phoneNumber);

        // Act
        var result = await SendAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Verify code was saved in database
        var user = await GetUserByPhoneAsync(phoneNumber);
        user.Should().NotBeNull();
        user!.VerificationCode.Should().NotBeNullOrEmpty();
        user.VerificationCode.Should().HaveLength(6); // Default code length
        user.VerificationCodeExpiry.Should().NotBeNull();
        user.VerificationCodeExpiry.Should().BeAfter(DateTime.UtcNow);
        user.LastCodeSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetVerificationCodeByPhone_WithNonExistentPhone_ShouldThrowNotFoundException()
    {
        // Arrange
        var request = new GetVerificationCodeByPhoneCommand("+9999999999");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(request)
        );

        // Check for localization key or English message
        exception.Message.Should().Match(msg => 
            msg.Contains("exception_user_not_found") || 
            msg.Contains("No user found"));
    }

    [Fact]
    public async Task GetVerificationCodeByPhone_WithInvalidPhoneFormat_ShouldThrowValidationException()
    {
        // Arrange
        var request = new GetVerificationCodeByPhoneCommand("invalid-phone");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    [Fact]
    public async Task GetVerificationCodeByPhone_WithDisabledUser_ShouldThrowForbiddenException()
    {
        // Arrange
        var phoneNumber = "+1234567891";
        await CreateTestUserWithPhoneAsync(phoneNumber, isActive: false);

        var request = new GetVerificationCodeByPhoneCommand(phoneNumber);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task GetVerificationCodeByPhone_WithLockedOutUser_ShouldThrowForbiddenException()
    {
        // Arrange
        var phoneNumber = "+1234567892";
        await CreateTestUserWithPhoneAsync(
            phoneNumber, 
            codeLockoutUntil: DateTime.UtcNow.AddMinutes(15));

        var request = new GetVerificationCodeByPhoneCommand(phoneNumber);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("locked");
    }

    [Fact]
    public async Task GetVerificationCodeByPhone_WithinCooldownPeriod_ShouldThrowBadRequestException()
    {
        // Arrange
        var phoneNumber = "+1234567893";
        await CreateTestUserWithPhoneAsync(
            phoneNumber,
            lastCodeSentAt: DateTime.UtcNow.AddSeconds(-30)); // 30 seconds ago (cooldown is 60s)

        var request = new GetVerificationCodeByPhoneCommand(phoneNumber);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("wait");
    }

    #endregion

    #region Get Verification Code By Email Tests

    [Fact]
    public async Task GetVerificationCodeByEmail_WithValidEmail_ShouldReturnTrue()
    {
        // Arrange
        var email = "otptest@example.com";
        await CreateTestUserAsync(email: email);

        var request = new GetVerificationCodeByEmailCommand(email);

        // Act
        var result = await SendAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Verify code was saved in database
        var user = await GetUserByEmailAsync(email);
        user.Should().NotBeNull();
        user!.VerificationCode.Should().NotBeNullOrEmpty();
        user.VerificationCode.Should().HaveLength(6);
        user.VerificationCodeExpiry.Should().NotBeNull();
        user.VerificationCodeExpiry.Should().BeAfter(DateTime.UtcNow);
        user.LastCodeSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetVerificationCodeByEmail_WithNonExistentEmail_ShouldThrowNotFoundException()
    {
        // Arrange
        var request = new GetVerificationCodeByEmailCommand("nonexistent@example.com");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(request)
        );

        // Check for localization key or English message
        exception.Message.Should().Match(msg => 
            msg.Contains("exception_user_not_found") || 
            msg.Contains("No user found"));
    }

    [Fact]
    public async Task GetVerificationCodeByEmail_WithInvalidEmailFormat_ShouldThrowValidationException()
    {
        // Arrange
        var request = new GetVerificationCodeByEmailCommand("invalid-email");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    [Fact]
    public async Task GetVerificationCodeByEmail_WithDisabledUser_ShouldThrowForbiddenException()
    {
        // Arrange
        var email = "disabled@example.com";
        await CreateTestUserAsync(email: email, isActive: false);

        var request = new GetVerificationCodeByEmailCommand(email);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("disabled");
    }

    #endregion

    #region Login With Code By Phone Tests

    [Fact]
    public async Task LoginWithCodeByPhone_WithValidCode_ShouldReturnTokens()
    {
        // Arrange
        var phoneNumber = "+1234567894";
        var verificationCode = "123456";
        await CreateTestUserWithPhoneAsync(
            phoneNumber,
            verificationCode: verificationCode,
            codeExpiry: DateTime.UtcNow.AddMinutes(5));

        var request = new LoginWithCodeByPhoneCommand(phoneNumber, verificationCode);

        // Act
        var result = await SendAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.PhoneNumber.Should().Be(phoneNumber);

        // Verify code was cleared after successful login
        var user = await GetUserByPhoneAsync(phoneNumber);
        user!.VerificationCode.Should().BeNull();
        user.VerificationCodeExpiry.Should().BeNull();
        user.FailedCodeAttempts.Should().Be(0);
        user.CodeLockoutUntil.Should().BeNull();
    }

    [Fact]
    public async Task LoginWithCodeByPhone_WithWrongCode_ShouldIncrementFailedAttempts()
    {
        // Arrange
        var phoneNumber = "+1234567895";
        await CreateTestUserWithPhoneAsync(
            phoneNumber,
            verificationCode: "123456",
            codeExpiry: DateTime.UtcNow.AddMinutes(5));

        var request = new LoginWithCodeByPhoneCommand(phoneNumber, "999999");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("incorrect");
        exception.Message.Should().Contain("remaining");

        // Verify failed attempts were incremented
        var user = await GetUserByPhoneAsync(phoneNumber);
        user!.FailedCodeAttempts.Should().Be(1);
    }

    [Fact]
    public async Task LoginWithCodeByPhone_WithMaxFailedAttempts_ShouldLockAccount()
    {
        // Arrange
        var phoneNumber = "+1234567896";
        await CreateTestUserWithPhoneAsync(
            phoneNumber,
            verificationCode: "123456",
            codeExpiry: DateTime.UtcNow.AddMinutes(5),
            failedAttempts: 2); // One more attempt will trigger lockout (max is 3)

        var request = new LoginWithCodeByPhoneCommand(phoneNumber, "999999");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("Too many failed attempts");
        exception.Message.Should().Contain("locked");

        // Verify account was locked
        var user = await GetUserByPhoneAsync(phoneNumber);
        user!.CodeLockoutUntil.Should().NotBeNull();
        user.CodeLockoutUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginWithCodeByPhone_WithExpiredCode_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var phoneNumber = "+1234567897";
        await CreateTestUserWithPhoneAsync(
            phoneNumber,
            verificationCode: "123456",
            codeExpiry: DateTime.UtcNow.AddMinutes(-5)); // Expired 5 minutes ago

        var request = new LoginWithCodeByPhoneCommand(phoneNumber, "123456");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task LoginWithCodeByPhone_WithInvalidCodeLength_ShouldThrowValidationException()
    {
        // Arrange
        var phoneNumber = "+1234567898";
        var request = new LoginWithCodeByPhoneCommand(phoneNumber, "12345"); // 5 digits instead of 6

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    [Fact]
    public async Task LoginWithCodeByPhone_WithNonNumericCode_ShouldThrowValidationException()
    {
        // Arrange
        var phoneNumber = "+1234567899";
        var request = new LoginWithCodeByPhoneCommand(phoneNumber, "abc123"); // Contains letters

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    #endregion

    #region Login With Code By Email Tests

    [Fact]
    public async Task LoginWithCodeByEmail_WithValidCode_ShouldReturnTokens()
    {
        // Arrange
        var email = "loginotp@example.com";
        var verificationCode = "123456";
        await CreateTestUserAsync(
            email: email,
            verificationCode: verificationCode,
            codeExpiry: DateTime.UtcNow.AddMinutes(5));

        var request = new LoginWithCodeByEmailCommand(email, verificationCode);

        // Act
        var result = await SendAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.Email.Should().Be(email);

        // Verify code was cleared after successful login
        var user = await GetUserByEmailAsync(email);
        user!.VerificationCode.Should().BeNull();
        user.VerificationCodeExpiry.Should().BeNull();
        user.FailedCodeAttempts.Should().Be(0);
    }

    [Fact]
    public async Task LoginWithCodeByEmail_WithWrongCode_ShouldIncrementFailedAttempts()
    {
        // Arrange
        var email = "loginotp2@example.com";
        await CreateTestUserAsync(
            email: email,
            verificationCode: "123456",
            codeExpiry: DateTime.UtcNow.AddMinutes(5));

        var request = new LoginWithCodeByEmailCommand(email, "999999");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("incorrect");

        // Verify failed attempts were incremented
        var user = await GetUserByEmailAsync(email);
        user!.FailedCodeAttempts.Should().Be(1);
    }

    [Fact]
    public async Task LoginWithCodeByEmail_WithMaxFailedAttempts_ShouldLockAccount()
    {
        // Arrange
        var email = "loginotp3@example.com";
        await CreateTestUserAsync(
            email: email,
            verificationCode: "123456",
            codeExpiry: DateTime.UtcNow.AddMinutes(5),
            failedAttempts: 2); // One more will trigger lockout

        var request = new LoginWithCodeByEmailCommand(email, "999999");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("Too many failed attempts");

        // Verify account was locked
        var user = await GetUserByEmailAsync(email);
        user!.CodeLockoutUntil.Should().NotBeNull();
    }

    [Fact]
    public async Task LoginWithCodeByEmail_WithExpiredCode_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var email = "loginotp4@example.com";
        await CreateTestUserAsync(
            email: email,
            verificationCode: "123456",
            codeExpiry: DateTime.UtcNow.AddMinutes(-5));

        var request = new LoginWithCodeByEmailCommand(email, "123456");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(request)
        );

        exception.Message.Should().Contain("expired");
    }

    #endregion

    #region Register With Code By Phone Tests

    [Fact]
    public async Task RegisterWithCodeByPhone_WithValidData_ShouldReturnTrue()
    {
        // Arrange
        var phoneNumber = "+1987654321";
        var request = new RegisterWithCodeByPhoneCommand(
            PhoneNumber: phoneNumber,
            FirstName: "John",
            LastName: "Doe");

        // Act
        var result = await SendAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Verify user was created with verification code
        var user = await GetUserByPhoneAsync(phoneNumber);
        user.Should().NotBeNull();
        user!.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.PhoneNumber.Should().Be(phoneNumber);
        user.Email.Should().BeNull(); // No email for phone registration
        user.VerificationCode.Should().NotBeNullOrEmpty();
        user.VerificationCodeExpiry.Should().NotBeNull();
        user.PasswordHash.Should().BeNullOrEmpty(); // No password for OTP-based registration
    }

    [Fact]
    public async Task RegisterWithCodeByPhone_WithDataProperty_ShouldCreateUserWithData()
    {
        // Arrange
        var phoneNumber = "+1987654340";
        var testData = "{\"source\": \"mobile_app\", \"version\": \"1.0\", \"deviceId\": \"abc123\"}";
        var request = new RegisterWithCodeByPhoneCommand(
            PhoneNumber: phoneNumber,
            FirstName: "John",
            LastName: "Doe",
            Data: testData);

        // Act
        var result = await SendAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Verify user was created with data property
        var user = await GetUserByPhoneAsync(phoneNumber);
        user.Should().NotBeNull();
        user!.Data.Should().Be(testData);
        user.PhoneNumber.Should().Be(phoneNumber);
    }

    [Fact]
    public async Task RegisterWithCodeByPhone_WithExistingPhone_ShouldThrowConflictException()
    {
        // Arrange
        var phoneNumber = "+1987654322";
        await CreateTestUserWithPhoneAsync(phoneNumber);

        var request = new RegisterWithCodeByPhoneCommand(
            PhoneNumber: phoneNumber,
            FirstName: "Jane",
            LastName: "Doe");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(
            async () => await SendAsync(request)
        );

        // Check for localization key or English message
        exception.Message.Should().Match(msg => 
            msg.Contains("exception_phone_already_registered") || 
            msg.Contains("already registered"));
    }

    [Fact]
    public async Task RegisterWithCodeByPhone_WithInvalidPhoneFormat_ShouldThrowValidationException()
    {
        // Arrange
        var request = new RegisterWithCodeByPhoneCommand(
            PhoneNumber: "invalid-phone",
            FirstName: "John",
            LastName: "Doe");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    [Fact]
    public async Task RegisterWithCodeByPhone_WithEmptyFirstName_ShouldThrowValidationException()
    {
        // Arrange
        var request = new RegisterWithCodeByPhoneCommand(
            PhoneNumber: "+1987654323",
            FirstName: "",
            LastName: "Doe");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    [Fact]
    public async Task RegisterWithCodeByPhone_WithInvalidFirstName_ShouldThrowValidationException()
    {
        // Arrange
        var request = new RegisterWithCodeByPhoneCommand(
            PhoneNumber: "+1987654324",
            FirstName: "John123", // Contains numbers
            LastName: "Doe");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    #endregion

    #region Register With Code By Email Tests

    [Fact]
    public async Task RegisterWithCodeByEmail_WithValidData_ShouldReturnTrue()
    {
        // Arrange
        var email = "newuser@example.com";
        var request = new RegisterWithCodeByEmailCommand(
            Email: email,
            FirstName: "Alice",
            LastName: "Smith");

        // Act
        var result = await SendAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Verify user was created with verification code
        var user = await GetUserByEmailAsync(email);
        user.Should().NotBeNull();
        user!.FirstName.Should().Be("Alice");
        user.LastName.Should().Be("Smith");
        user.Email.Should().Be(email);
        user.PhoneNumber.Should().BeNull(); // No phone for email registration
        user.VerificationCode.Should().NotBeNullOrEmpty();
        user.VerificationCodeExpiry.Should().NotBeNull();
        user.PasswordHash.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterWithCodeByEmail_WithDataProperty_ShouldCreateUserWithData()
    {
        // Arrange
        var email = "userwithdata@example.com";
        var testData = "{\"referralCode\": \"REF123\", \"campaign\": \"summer2025\", \"terms\": \"v2.0\"}";
        var request = new RegisterWithCodeByEmailCommand(
            Email: email,
            FirstName: "Alice",
            LastName: "Smith",
            Data: testData);

        // Act
        var result = await SendAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Verify user was created with data property
        var user = await GetUserByEmailAsync(email);
        user.Should().NotBeNull();
        user!.Data.Should().Be(testData);
        user.Email.Should().Be(email);
    }

    [Fact]
    public async Task RegisterWithCodeByEmail_WithExistingEmail_ShouldThrowConflictException()
    {
        // Arrange
        var email = "existing@example.com";
        await CreateTestUserAsync(email: email);

        var request = new RegisterWithCodeByEmailCommand(
            Email: email,
            FirstName: "Bob",
            LastName: "Johnson");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(
            async () => await SendAsync(request)
        );

        // Check for localization key or English message
        exception.Message.Should().Match(msg => 
            msg.Contains("exception_email_already_exists") || 
            msg.Contains("already registered"));
    }

    [Fact]
    public async Task RegisterWithCodeByEmail_WithInvalidEmailFormat_ShouldThrowValidationException()
    {
        // Arrange
        var request = new RegisterWithCodeByEmailCommand(
            Email: "invalid-email",
            FirstName: "Charlie",
            LastName: "Brown");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    [Fact]
    public async Task RegisterWithCodeByEmail_WithEmptyLastName_ShouldThrowValidationException()
    {
        // Arrange
        var request = new RegisterWithCodeByEmailCommand(
            Email: "test@example.com",
            FirstName: "David",
            LastName: "");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    [Fact]
    public async Task RegisterWithCodeByEmail_WithInvalidLastName_ShouldThrowValidationException()
    {
        // Arrange
        var request = new RegisterWithCodeByEmailCommand(
            Email: "test2@example.com",
            FirstName: "Emma",
            LastName: "Smith@123"); // Contains special characters

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(request)
        );
    }

    #endregion

    #region OTP Security Flow Tests

    [Fact]
    public async Task OtpSecurityFlow_CompleteRegistrationAndLoginWithPhone_ShouldWork()
    {
        // Arrange
        var phoneNumber = "+1555555555";

        // Step 1: Register with phone
        var registerCommand = new RegisterWithCodeByPhoneCommand(
            PhoneNumber: phoneNumber,
            FirstName: "Test",
            LastName: "User");

        var registerResult = await SendAsync(registerCommand);
        registerResult.Success.Should().BeTrue();

        // Get the generated verification code
        var user = await GetUserByPhoneAsync(phoneNumber);
        var verificationCode = user!.VerificationCode;

        // Step 2: Login with the verification code
        var loginCommand = new LoginWithCodeByPhoneCommand(phoneNumber, verificationCode!);
        var loginResult = await SendAsync(loginCommand);

        // Assert
        loginResult.Should().NotBeNull();
        loginResult.AccessToken.Should().NotBeNullOrEmpty();
        loginResult.RefreshToken.Should().NotBeNullOrEmpty();
        loginResult.PhoneNumber.Should().Be(phoneNumber);
    }

    [Fact]
    public async Task OtpSecurityFlow_CompleteRegistrationAndLoginWithEmail_ShouldWork()
    {
        // Arrange
        var email = "flowtest@example.com";

        // Step 1: Register with email
        var registerCommand = new RegisterWithCodeByEmailCommand(
            Email: email,
            FirstName: "Flow",
            LastName: "Test");

        var registerResult = await SendAsync(registerCommand);
        registerResult.Success.Should().BeTrue();

        // Get the generated verification code
        var user = await GetUserByEmailAsync(email);
        var verificationCode = user!.VerificationCode;

        // Step 2: Login with the verification code
        var loginCommand = new LoginWithCodeByEmailCommand(email, verificationCode!);
        var loginResult = await SendAsync(loginCommand);

        // Assert
        loginResult.Should().NotBeNull();
        loginResult.AccessToken.Should().NotBeNullOrEmpty();
        loginResult.RefreshToken.Should().NotBeNullOrEmpty();
        loginResult.Email.Should().Be(email);
    }

    [Fact]
    public async Task OtpSecurityFlow_GetNewCodeAndLogin_ShouldWork()
    {
        // Arrange
        var phoneNumber = "+1666666666";
        await CreateTestUserWithPhoneAsync(phoneNumber);

        // Step 1: Get verification code
        await Task.Delay(1100); // Wait for cooldown (ensure > 1 second)
        var getCodeCommand = new GetVerificationCodeByPhoneCommand(phoneNumber);
        var getCodeResult = await SendAsync(getCodeCommand);
        getCodeResult.Success.Should().BeTrue();

        // Get the generated code
        var user = await GetUserByPhoneAsync(phoneNumber);
        var verificationCode = user!.VerificationCode;

        // Step 2: Login with code
        var loginCommand = new LoginWithCodeByPhoneCommand(phoneNumber, verificationCode!);
        var loginResult = await SendAsync(loginCommand);

        // Assert
        loginResult.Should().NotBeNull();
        loginResult.AccessToken.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Create a test user with phone number and optional OTP fields
    /// </summary>
    private async Task<User> CreateTestUserWithPhoneAsync(
        string phoneNumber,
        string? verificationCode = null,
        DateTime? codeExpiry = null,
        int failedAttempts = 0,
        DateTime? codeLockoutUntil = null,
        DateTime? lastCodeSentAt = null,
        bool isActive = true)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var user = new User
            {
                PhoneNumber = phoneNumber,
                FirstName = "Test",
                LastName = "User",
                Created = DateTime.UtcNow,
                IsArchived = false,
                Status = isActive,
                VerificationCode = verificationCode,
                VerificationCodeExpiry = codeExpiry,
                FailedCodeAttempts = failedAttempts,
                CodeLockoutUntil = codeLockoutUntil,
                LastCodeSentAt = lastCodeSentAt
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        });
    }

    /// <summary>
    /// Create a test user with email and optional OTP fields
    /// </summary>
    private async Task<User> CreateTestUserAsync(
        string email,
        string? verificationCode = null,
        DateTime? codeExpiry = null,
        int failedAttempts = 0,
        DateTime? codeLockoutUntil = null,
        DateTime? lastCodeSentAt = null,
        bool isActive = true,
        string password = "Test123!")
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var user = new User
            {
                Email = email,
                PasswordHash = string.IsNullOrEmpty(password) ? null : BCrypt.Net.BCrypt.HashPassword(password),
                FirstName = "Test",
                LastName = "User",
                Created = DateTime.UtcNow,
                IsArchived = false,
                Status = isActive,
                VerificationCode = verificationCode,
                VerificationCodeExpiry = codeExpiry,
                FailedCodeAttempts = failedAttempts,
                CodeLockoutUntil = codeLockoutUntil,
                LastCodeSentAt = lastCodeSentAt
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        });
    }

    /// <summary>
    /// Get user by phone number
    /// </summary>
    private async Task<User?> GetUserByPhoneAsync(string phoneNumber)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            return await context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        });
    }

    /// <summary>
    /// Get user by email
    /// </summary>
    private async Task<User?> GetUserByEmailAsync(string email)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            return await context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
        });
    }

    #endregion
}


