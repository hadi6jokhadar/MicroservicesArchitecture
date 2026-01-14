using Identity.Infrastructure.Middleware;
using Microsoft.AspNetCore.Builder;

namespace Identity.Infrastructure.Extensions;

public static class DatabaseSeederExtensions
{
    /// <summary>
    /// Adds middleware that automatically seeds default roles and claims for each tenant/database
    /// Should be called after database migration middlewares
    /// </summary>
    public static IApplicationBuilder UseDatabaseSeeding(this IApplicationBuilder app)
    {
        return app.UseMiddleware<DatabaseSeederMiddleware>();
    }
}
