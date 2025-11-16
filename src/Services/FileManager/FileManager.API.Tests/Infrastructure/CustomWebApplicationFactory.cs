using FileManager.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileManager.API.Tests.Infrastructure;

/// <summary>
/// Custom web application factory for FileManager API integration tests
/// Inherits from shared testing base for common functionality
/// </summary>
public class CustomWebApplicationFactory : IhsanDev.Shared.Testing.Infrastructure.CustomWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory()
    {
        // CRITICAL: Configure Npgsql to handle all DateTime as UTC
        // This MUST be set before any Npgsql operations to ensure proper timezone handling
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
        
        // Set database provider - change this to switch between SQLite and PostgreSQL
        UsePostgreSQL = true;  // Set to true to use PostgreSQL for tests
        
        // Optional: Customize PostgreSQL connection string
        // PostgreSqlConnectionString = "Host=localhost;Port=5432;Database=filemanager_test;Username=postgres;Password=postgres;";
    }
    
    protected override Dictionary<string, string?> GetTestConfiguration()
    {
        var config = base.GetTestConfiguration();
        
        // Add FileManager-specific configuration
        config["Jwt:Secret"] = "test-super-secret-jwt-key-minimum-32-characters-long";
        config["Jwt:Issuer"] = "TestFileManagerService";
        config["Jwt:Audience"] = "TestMicroservicesApp";
        config["Jwt:AccessTokenExpirationMinutes"] = "60";
        config["Jwt:RefreshTokenExpirationDays"] = "7";
        
        // Disable multi-tenancy for testing
        config["MultiTenancy:Enabled"] = "false";
        
        // FileManager-specific configuration
        config["FileManagerOptions:RootStoragePath"] = "https://localhost:5005";
        config["FileManagerOptions:FilesSavePath"] = Path.Combine(Path.GetTempPath(), "FileManagerTests");
        config["FileManagerOptions:MaxFileSizeBytes"] = "104857600"; // 100 MB
        config["FileManagerOptions:AllowedExtensions:0"] = ".jpg";
        config["FileManagerOptions:AllowedExtensions:1"] = ".png";
        config["FileManagerOptions:AllowedExtensions:2"] = ".pdf";
        config["FileManagerOptions:AllowedExtensions:3"] = ".txt";
        config["FileManagerOptions:AllowedExtensions:4"] = ".mp3";
        config["FileManagerOptions:AllowedExtensions:5"] = ".mp4";
        config["FileManagerOptions:ExtensionToTypeMapping:.jpg"] = "Image";
        config["FileManagerOptions:ExtensionToTypeMapping:.png"] = "Image";
        config["FileManagerOptions:ExtensionToTypeMapping:.pdf"] = "Other";
        config["FileManagerOptions:ExtensionToTypeMapping:.txt"] = "Other";
        config["FileManagerOptions:ExtensionToTypeMapping:.mp3"] = "Music";
        config["FileManagerOptions:ExtensionToTypeMapping:.mp4"] = "Video";
        
        return config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        builder.ConfigureServices(services =>
        {
            // Configure the FileManagerDbContext
            ConfigureDbContext<FileManagerDbContext>(services);

            // Build service provider and initialize database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FileManagerDbContext>();
            
            InitializeDatabase(dbContext);
            SeedTestData(dbContext);
        });
    }

    protected override void SeedTestData<TDbContext>(TDbContext context)
    {
        // FileManager-specific seed data if needed
        context.SaveChanges();
    }
}
