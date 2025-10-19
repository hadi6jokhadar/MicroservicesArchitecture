using System.Net.Http.Headers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IhsanDev.Shared.Testing.Infrastructure;

/// <summary>
/// Generic base class for integration tests providing common utilities
/// Tests use MediatR handlers directly to bypass .NET 9.0 PipeWriter bug
/// </summary>
/// <typeparam name="TDbContext">The DbContext type for the service being tested</typeparam>
/// <typeparam name="TFactory">The WebApplicationFactory type</typeparam>
public abstract class IntegrationTestBase<TDbContext, TFactory> : IDisposable
    where TDbContext : DbContext
    where TFactory : class
{
    protected readonly CustomWebApplicationFactory<TFactory> Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(CustomWebApplicationFactory<TFactory> factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Execute MediatR command/query within a scope
    /// This bypasses HTTP layer and tests handlers directly, avoiding .NET 9 PipeWriter bug
    /// </summary>
    protected async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request);
    }

    /// <summary>
    /// Execute database operations within a scope
    /// </summary>
    protected async Task ExecuteDbContextAsync(Func<TDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await action(context);
    }

    /// <summary>
    /// Execute database operations with return value
    /// </summary>
    protected async Task<T> ExecuteDbContextAsync<T>(Func<TDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await action(context);
    }

    /// <summary>
    /// Set authorization header with bearer token
    /// </summary>
    protected void SetAuthorizationHeader(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Clear authorization header
    /// </summary>
    protected void ClearAuthorizationHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Generate unique email for testing
    /// </summary>
    protected string GenerateUniqueEmail(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}@example.com";
    }

    /// <summary>
    /// Generate unique string for testing
    /// </summary>
    protected string GenerateUniqueString(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    public virtual void Dispose()
    {
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
