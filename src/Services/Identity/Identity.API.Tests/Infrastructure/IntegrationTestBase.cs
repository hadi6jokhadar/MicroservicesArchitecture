using System.Net.Http.Headers;
using Identity.Application.Commands;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using IhsanDev.Shared.Kernel.Enums.Identity;
using IhsanDev.Shared.Testing.Infrastructure;

namespace Identity.API.Tests.Infrastructure;

/// <summary>
/// Base class for Identity API integration tests
/// Inherits from shared testing base and adds Identity-specific helpers
/// </summary>
public abstract class IntegrationTestBase : 
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<IdentityDbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// Authenticate and get JWT token for testing (uses MediatR)
    /// </summary>
    protected async Task<string> GetAuthTokenAsync(string email = "user@example.com", string password = "User123!")
    {
        var loginCommand = new LoginCommand(email, password);
        var result = await SendAsync(loginCommand);
        return result.AccessToken ?? throw new Exception("No access token returned");
    }

    /// <summary>
    /// Get admin auth token
    /// </summary>
    protected async Task<string> GetAdminTokenAsync()
    {
        return await GetAuthTokenAsync("admin@example.com", "Admin123!");
    }

    /// <summary>
    /// Create a test user with unique email
    /// </summary>
    protected async Task<User> CreateTestUserAsync(
        string? email = null,
        string password = "Test123!",
        string firstName = "Test",
        string lastName = "User",
        UserRole role = UserRole.User)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var user = new User
            {
                Email = email ?? GenerateUniqueEmail("testuser"),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                Created = DateTime.UtcNow,
                IsArchived = false,
                Status = true
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        });
    }
}
