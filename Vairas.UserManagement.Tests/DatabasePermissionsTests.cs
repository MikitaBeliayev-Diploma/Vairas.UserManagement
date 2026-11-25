using System.Net;
using Npgsql;
using NUnit.Framework;

namespace Vairas.UserManagement.Tests;

[TestFixture, Category("Integration"), NonParallelizable]
public sealed class DatabasePermissionsTests
{
    [Test]
    public async Task MigrationOwnerProvisioningDeniesRuntimeCreateAndReportsHealthy()
    {
        await using var environment = new TestEnvironment();
        await environment.StartAsync();
        await using var owner = new NpgsqlConnection(environment.Database.MigrationConnection);
        await owner.OpenAsync();
        await using var ownership = new NpgsqlCommand("SELECT bool_and(tableowner=current_user) FROM pg_tables WHERE schemaname='public'", owner);
        Assert.That(await ownership.ExecuteScalarAsync(), Is.EqualTo(true), "Migrations must run as the real migration role, not the administrator.");
        await AssertRuntimeRestricted(environment.Database);
        using var ready = await environment.Client.GetAsync("/health/ready");
        Assert.That(ready.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("schema-direct")]
    [TestCase("schema-public")]
    [TestCase("database-create")]
    [TestCase("temporary")]
    [TestCase("owner-membership")]
    [TestCase("role-createdb")]
    public async Task ReadinessRejectsUnsafeEffectivePrivileges(string privilege)
    {
        await using var environment = new TestEnvironment();
        await environment.StartAsync();
        var database = environment.Database;
        var name = new NpgsqlConnectionStringBuilder(database.GetConnectionString()).Database!;
        var grant = privilege switch
        {
            "schema-direct" => $"GRANT CREATE ON SCHEMA public TO {database.RuntimeRole}",
            "schema-public" => "GRANT CREATE ON SCHEMA public TO PUBLIC",
            "database-create" => $"GRANT CREATE ON DATABASE {name} TO {database.RuntimeRole}",
            "temporary" => $"GRANT TEMP ON DATABASE {name} TO {database.RuntimeRole}",
            "owner-membership" => $"GRANT {database.MigrationRole} TO {database.RuntimeRole}",
            _ => $"ALTER ROLE {database.RuntimeRole} CREATEDB"
        };
        await database.ExecuteAdminAsync(grant);
        using var ready = await environment.Client.GetAsync("/health/ready");
        Assert.That(ready.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task GrantsRemoveDirectCreateAndTemporaryPrivileges()
    {
        await using var environment = new TestEnvironment();
        await environment.StartAsync();
        var database = environment.Database;
        var name = new NpgsqlConnectionStringBuilder(database.GetConnectionString()).Database!;
        await database.ExecuteMigrationAsync($"GRANT CREATE ON SCHEMA public TO {database.RuntimeRole}; GRANT CREATE,TEMP ON DATABASE {name} TO {database.RuntimeRole}");
        await database.ApplyRuntimeGrantsAsync();
        await AssertRuntimeRestricted(database);
    }

    [Test]
    public async Task GrantsFailWhenAdministratorPreparationWasOmitted()
    {
        await using var database = new TestDatabase();
        await database.StartAsync(prepareSchema: false);
        var error = Assert.ThrowsAsync<PostgresException>(() => database.ApplyRuntimeGrantsAsync());
        Assert.That(error!.SqlState, Is.EqualTo("P0001"));
        Assert.That(error.MessageText, Does.Contain("prepare-schema.sql"));
    }

    [Test]
    public async Task GrantsFailWhenRuntimeCanAssumeMigrationOwner()
    {
        await using var environment = new TestEnvironment();
        await environment.StartAsync();
        var database = environment.Database;
        await database.ExecuteAdminAsync($"GRANT {database.MigrationRole} TO {database.RuntimeRole}");
        var error = Assert.ThrowsAsync<PostgresException>(() => database.ApplyRuntimeGrantsAsync());
        Assert.That(error!.SqlState, Is.EqualTo("P0001"));
        Assert.That(error.MessageText, Does.Contain("unsafe effective privileges"));
    }

    private static async Task AssertRuntimeRestricted(TestDatabase database)
    {
        await using var runtime = new NpgsqlConnection(database.RuntimeConnection);
        await runtime.OpenAsync();
        await using (var privileges = new NpgsqlCommand("""
            SELECT has_schema_privilege('public','CREATE'),
                   has_database_privilege(current_database(),'CREATE'),
                   has_database_privilege(current_database(),'TEMP'),
                   EXISTS (SELECT 1 FROM pg_tables WHERE schemaname='public'
                           AND pg_has_role(current_user,tableowner,'MEMBER'))
            """, runtime))
        await using (var reader = await privileges.ExecuteReaderAsync())
        {
            Assert.That(await reader.ReadAsync(), Is.True);
            for (var index = 0; index < 4; index++) Assert.That(reader.GetBoolean(index), Is.False, $"Privilege {index}");
        }
        await using var create = new NpgsqlCommand("CREATE TABLE public.runtime_must_not_create(id integer)", runtime);
        var denied = Assert.ThrowsAsync<PostgresException>(async () => await create.ExecuteNonQueryAsync());
        Assert.That(denied!.SqlState, Is.EqualTo(PostgresErrorCodes.InsufficientPrivilege));
    }
}
