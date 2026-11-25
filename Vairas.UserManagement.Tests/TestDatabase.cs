using Npgsql;
using System.Reflection;
using Testcontainers.PostgreSql;

namespace Vairas.UserManagement.Tests;

/// <summary>Docker by default, or isolated databases in an explicitly supplied disposable test cluster.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private PostgreSqlContainer? container;
    private string? administrator;
    private string connection = "";
    private bool rolesCreated;
    private readonly string database = "um_suite_" + Guid.NewGuid().ToString("N");
    public string RuntimeRole { get; } = "um_runtime_" + Guid.NewGuid().ToString("N");
    public string MigrationRole { get; } = "um_migration_" + Guid.NewGuid().ToString("N");
    public string MigrationConnection => new NpgsqlConnectionStringBuilder(connection) { Username = MigrationRole, Password = "test-only" }.ConnectionString;
    public string RuntimeConnection => new NpgsqlConnectionStringBuilder(connection) { Username = RuntimeRole, Password = "test-only" }.ConnectionString;
    public string GetConnectionString() => connection;
    public Task SetOutboxReadAsync(bool allowed) => ExecuteAsync(MigrationConnection,
        (allowed ? "GRANT SELECT ON outbox_message TO " : "REVOKE SELECT ON outbox_message FROM ") +
        new NpgsqlCommandBuilder().QuoteIdentifier(RuntimeRole));
    public async Task StartAsync(bool prepareSchema = true)
    {
        administrator = Environment.GetEnvironmentVariable("VAIRAS_TEST_ADMIN_CONNECTION");
        if (string.IsNullOrWhiteSpace(administrator))
        {
            container = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("postgres")
                .WithUsername("postgres").WithPassword("test-only").Build();
            await container.StartAsync();
            administrator = container.GetConnectionString();
        }
        await ExecuteAsync(administrator!, $"CREATE ROLE {MigrationRole} LOGIN PASSWORD 'test-only' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS NOREPLICATION NOINHERIT; CREATE ROLE {RuntimeRole} LOGIN PASSWORD 'test-only' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS NOREPLICATION NOINHERIT;");
        rolesCreated = true;
        await ExecuteAsync(administrator!, $"CREATE DATABASE {database} OWNER {MigrationRole}");
        connection = new NpgsqlConnectionStringBuilder(administrator) { Database = database }.ConnectionString;
        if (prepareSchema) await ExecuteAdminAsync(await ResourceAsync("PrepareSchema.sql"));
    }
    public Task ExecuteAdminAsync(string sql) => ExecuteAsync(connection, sql);
    public Task ExecuteMigrationAsync(string sql) => ExecuteAsync(MigrationConnection, sql);
    public async Task ApplyRuntimeGrantsAsync() => await ExecuteMigrationAsync(await ResourceAsync("DatabaseRoles.sql"));
    private async Task<string> ResourceAsync(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return (await reader.ReadToEndAsync()).Replace("v_user_management_runtime", RuntimeRole).Replace("v_user_management_migration", MigrationRole);
    }
    private static async Task ExecuteAsync(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db); await command.ExecuteNonQueryAsync();
    }
    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        if (!string.IsNullOrWhiteSpace(administrator) && connection.Length != 0)
            await ExecuteAsync(administrator, $"DROP DATABASE IF EXISTS {database} WITH (FORCE)");
        if (!string.IsNullOrWhiteSpace(administrator) && rolesCreated)
            await ExecuteAsync(administrator, $"DROP ROLE IF EXISTS {RuntimeRole}; DROP ROLE IF EXISTS {MigrationRole}");
        if (container is not null) await container.DisposeAsync();
    }
}
