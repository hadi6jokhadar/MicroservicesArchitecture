using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Nasheed.Infrastructure.Persistence;

public class NasheedDbContextFactory : IDesignTimeDbContextFactory<NasheedDbContext>
{
    public NasheedDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Nasheed.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<NasheedDbContext>();
        var connectionString = configuration["DatabaseSettings:ConnectionString"]
            ?? "Host=localhost;Database=nasheed_global;Username=postgres;Password=postgres";
        var provider = configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
            optionsBuilder.UseNpgsql(connectionString);
        else
            optionsBuilder.UseSqlite(connectionString);

        return new NasheedDbContext(optionsBuilder.Options);
    }
}
