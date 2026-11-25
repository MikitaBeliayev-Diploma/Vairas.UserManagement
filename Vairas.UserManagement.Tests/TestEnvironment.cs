using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Integrations;
using Vairas.UserManagement.Persistance.DataAccess;
using Vairas.UserManagement.Security;
using Vairas.UserManagement.Workers;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Tests;

public sealed class TestEnvironment : IAsyncDisposable
{
    public static readonly Guid OperatorIdentity = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public readonly TestDatabase Database = new();
    public readonly DependencyServer Dependencies = new();
    public TestApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public Guid Identity { get; set; }
    public string Token { get; set; } = "";

    public async Task StartAsync()
    {
        await Database.StartAsync();
        await Dependencies.StartAsync();
        var migrationOptions = new DbContextOptionsBuilder<VairasDbContext>();
        migrationOptions.UseVairasPostgres(Database.MigrationConnection);
        await using (var db = new VairasDbContext(migrationOptions.Options))
        {
            await db.Database.MigrateAsync();
            await db.Database.OpenConnectionAsync();
            await ((NpgsqlConnection)db.Database.GetDbConnection()).ReloadTypesAsync();
        }
        await Database.ApplyRuntimeGrantsAsync();
        Factory = new(Database.RuntimeConnection, Dependencies, OperatorIdentity);
        Client = Factory.CreateClient();
    }

    public VairasDbContext AdminDb()
    {
        var options = new DbContextOptionsBuilder<VairasDbContext>();
        options.UseVairasPostgres(Database.GetConnectionString());
        return new(options.Options);
    }
    public async Task ResetAsync()
    {
        await using var db = AdminDb();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE outbox_message,profile_operation,administrator_profile,teacher_profile,student_profile,user_profile");
        Dependencies.SessionActive = Dependencies.SchoolAllowed = Dependencies.ConfirmationAllowed = true;
        Dependencies.Unavailable = Dependencies.ExpiredAuthority = Dependencies.WrongAuthorityTarget = Dependencies.ExpiredConfirmation = Dependencies.WrongConfirmationFingerprint = false;
        Identity = Guid.NewGuid();
        Token = Dependencies.Token(Identity.ToString());
    }
    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, Guid? operationId = null,
        string? token = null, string? delegatedToken = null)
    {
        var message = new HttpRequestMessage(method, path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? Token);
        message.Headers.Add("X-Correlation-ID", "integration-test");
        if (operationId.HasValue) message.Headers.Add("Idempotency-Key", operationId.Value.ToString());
        if (delegatedToken is not null) message.Headers.Add("X-User-Authorization", "Bearer " + delegatedToken);
        if (body is not null) message.Content = JsonContent.Create(body);
        return Client.SendAsync(message);
    }
    public Task<HttpResponseMessage> CreateAsync(Guid? op = null, string? token = null) => SendAsync(HttpMethod.Post,
        "/api/v1/users/me", new { firstName = "Synthetic", lastName = "User", emailAddress = (string?)null, phoneNumber = (string?)null }, op ?? Guid.NewGuid(), token);
    public string Workload(string client, string scope) => Dependencies.Token(client, "vairas-user-management-internal", scope);
    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null) await Factory.DisposeAsync();
        await Dependencies.DisposeAsync();
        await Database.DisposeAsync();
    }
}
