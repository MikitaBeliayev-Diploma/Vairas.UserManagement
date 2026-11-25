using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vairas.UserManagement.Persistance.DataAccess;

namespace Vairas.UserManagement.HealthChecks;

public sealed class DatabaseReadiness(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VairasDbContext>();
            var restricted = await db.Database.SqlQueryRaw<bool>("""
                SELECT (NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
                  AND NOT r.rolcreaterole AND NOT r.rolreplication
                  AND NOT has_schema_privilege('public','CREATE')
                  AND NOT has_database_privilege(current_database(),'CREATE')
                  AND NOT has_database_privilege(current_database(),'TEMP')
                  AND NOT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname='public'
                                  AND pg_has_role(current_user,tableowner,'MEMBER'))) AS "Value"
                FROM pg_roles r WHERE r.rolname=current_user
                """).SingleAsync(cancellationToken);
            if (!restricted) return HealthCheckResult.Unhealthy("Database is not ready.");
            await db.Users.AnyAsync(cancellationToken);
            await db.Students.AnyAsync(cancellationToken);
            await db.Teachers.AnyAsync(cancellationToken);
            await db.Administrators.AnyAsync(cancellationToken);
            await db.Operations.AnyAsync(cancellationToken);
            await db.Outbox.AnyAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return HealthCheckResult.Unhealthy("Database is not ready."); }
    }
}
