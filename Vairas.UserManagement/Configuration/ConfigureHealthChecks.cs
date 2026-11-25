using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Vairas.UserManagement.HealthChecks;

namespace Vairas.UserManagement.Configuration;

public static class ConfigureHealthChecks
{
    public static IServiceCollection AddServiceHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseReadiness>("database", tags: ["ready"]);
        return services;
    }

    public static WebApplication MapServiceHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        return app;
    }
}
