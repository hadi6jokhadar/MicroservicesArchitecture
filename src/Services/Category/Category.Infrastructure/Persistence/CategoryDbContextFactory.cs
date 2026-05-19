using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Category.Infrastructure.Persistence;

public class CategoryDbContextFactory : IDesignTimeDbContextFactory<CategoryDbContext>
{
    public CategoryDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Category.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<CategoryDbContext>();
        var connectionString = configuration["DatabaseSettings:ConnectionString"]
            ?? "Host=localhost;Database=category_global;Username=postgres;Password=postgres";
        var provider = configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
            optionsBuilder.UseNpgsql(connectionString);
        else
            optionsBuilder.UseSqlite(connectionString);

        return new CategoryDbContext(optionsBuilder.Options);
    }
}
