using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PolySnap.Infrastructure.Persistence;

public class PolySnapDbContextFactory : IDesignTimeDbContextFactory<PolySnapDbContext>
{
    public PolySnapDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../PolySnap.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<PolySnapDbContext>();
        var connectionString = configuration["DatabaseSettings:ConnectionString"]
            ?? "Host=localhost;Database=polysnap_global;Username=postgres;Password=postgres";
        var provider = configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
            optionsBuilder.UseNpgsql(connectionString);
        else
            optionsBuilder.UseSqlite(connectionString);

        return new PolySnapDbContext(optionsBuilder.Options);
    }
}
