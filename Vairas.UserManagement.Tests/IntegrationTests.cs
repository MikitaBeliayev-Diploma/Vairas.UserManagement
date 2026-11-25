using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
using Vairas.UserManagement.Persistance;
using Vairas.UserManagement.Persistance.DataAccess;

namespace Vairas.UserManagement.Tests;

[TestFixture]
[NonParallelizable]
public sealed class IntegrationTests
{
    private readonly TestEnvironment env = new();
    [OneTimeSetUp] public Task Start() => env.StartAsync();
    [SetUp] public Task Reset() => env.ResetAsync();
    [OneTimeTearDown] public async Task Stop() => await env.DisposeAsync();

    private async Task<Guid> CreateUser(string? token = null)
    {
        using var response = await env.CreateAsync(token: token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(response.Headers.Location, Is.Not.Null);
        return json.RootElement.GetProperty("targetId").GetGuid();
    }
    private static object Update(long revision = 1, string name = "Changed") =>
        new { firstName = name, lastName = "User", emailAddress = (string?)null, phoneNumber = "+37000000000", expectedRevision = revision };

    [Test]
    public async Task MigrationsAndSchemaAreUsableAndRepeatable()
    {
        await using var db = env.AdminDb();
        await db.Database.MigrateAsync();
        Assert.That((await db.Database.GetAppliedMigrationsAsync()).Count(), Is.EqualTo(2));
        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
        await using var connection = new NpgsqlConnection(env.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name IN ('user_profile','student_profile','teacher_profile','administrator_profile','profile_operation')", connection);
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(37L));
        await using var fk = new NpgsqlCommand("SELECT count(*) FROM pg_constraint WHERE contype='f' AND condeferrable", connection);
        Assert.That(await fk.ExecuteScalarAsync(), Is.EqualTo(3L));
    }

    [Test]
    public async Task CreatesReadsAndUpdatesProfileThroughRuntimeRole()
    {
        using (var scope = env.Factory.Services.CreateScope())
            Assert.That(await scope.ServiceProvider.GetRequiredService<VairasDbContext>().Users.AnyAsync(), Is.False);
        var id = await CreateUser();
        using var get = await env.SendAsync(HttpMethod.Get, "/api/v1/users/me");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(json.RootElement.GetProperty("userId").GetGuid(), Is.EqualTo(id));
        Assert.That(json.RootElement.GetProperty("emailAddress").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(json.RootElement.TryGetProperty("identityId", out _), Is.False);
        using var update = await env.SendAsync(HttpMethod.Put, "/api/v1/users/me", Update(), Guid.NewGuid());
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK), await update.Content.ReadAsStringAsync());
        await using var db = env.AdminDb();
        var user = await db.Users.SingleAsync();
        Assert.That(user.Revision, Is.EqualTo(2));
        Assert.That(user.FirstName, Is.EqualTo("Changed"));
    }

    [Test]
    public async Task ParallelRetriesCreateOnlyOneProfileAndOneOperation()
    {
        var operation = Guid.NewGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => env.CreateAsync(operation)));
        foreach (var response in responses) Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        var bodies = await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()));
        Assert.That(bodies.Distinct().Count(), Is.EqualTo(1));
        foreach (var response in responses) response.Dispose();
        await using var db = env.AdminDb();
        Assert.That(await db.Users.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.Operations.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task ParallelDifferentCommandsAtSameRevisionOnlyApplyOne()
    {
        await CreateUser();
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => env.SendAsync(HttpMethod.Put,
            "/api/v1/users/me", Update(name: "Name" + i), Guid.NewGuid())));
        Assert.That(responses.Count(x => x.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
        Assert.That(responses.Count(x => x.StatusCode == HttpStatusCode.Conflict), Is.EqualTo(4));
        foreach (var response in responses) response.Dispose();
        await using var db = env.AdminDb();
        Assert.That((await db.Users.SingleAsync()).Revision, Is.EqualTo(2));
        Assert.That(await db.Operations.CountAsync(x => x.Outcome == OperationOutcome.Rejected), Is.EqualTo(4));
    }

    [Test]
    public async Task RetryReturnsOriginalReceiptAndDifferentPayloadConflicts()
    {
        await CreateUser();
        var operation = Guid.NewGuid();
        using var original = await env.SendAsync(HttpMethod.Put, "/api/v1/users/me", Update(), operation);
        using var next = await env.SendAsync(HttpMethod.Put, "/api/v1/users/me", Update(2, "Later"), Guid.NewGuid());
        using var retry = await env.SendAsync(HttpMethod.Put, "/api/v1/users/me", Update(), operation);
        Assert.That(await retry.Content.ReadAsStringAsync(), Is.EqualTo(await original.Content.ReadAsStringAsync()));
        using var changed = await env.SendAsync(HttpMethod.Put, "/api/v1/users/me", Update(1, "Different"), operation);
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [TestCase("student")]
    [TestCase("teacher")]
    [TestCase("administrator")]
    public async Task RoleIdRemainsStableAcrossCommands(string role)
    {
        await CreateUser();
        object request = role == "student" ? new { } : new { schoolId = Guid.NewGuid(), invitationId = Guid.NewGuid() };
        using var first = await env.SendAsync(HttpMethod.Post, $"/api/v1/users/me/{role}-profile", request, Guid.NewGuid());
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created), await first.Content.ReadAsStringAsync());
        using var second = await env.SendAsync(HttpMethod.Post, $"/api/v1/users/me/{role}-profile", request, Guid.NewGuid());
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var get = await env.SendAsync(HttpMethod.Get, $"/api/v1/users/me/{role}-profile");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.That(firstJson.RootElement.GetProperty("targetId").GetGuid(), Is.EqualTo(secondJson.RootElement.GetProperty("targetId").GetGuid()));
    }

    [TestCase("denied")]
    [TestCase("wrong-target")]
    [TestCase("expired")]
    public async Task StaffProfileRequiresCurrentExactSchoolAuthority(string failure)
    {
        await CreateUser();
        env.Dependencies.SchoolAllowed = failure != "denied";
        env.Dependencies.WrongAuthorityTarget = failure == "wrong-target";
        env.Dependencies.ExpiredAuthority = failure == "expired";
        using var response = await env.SendAsync(HttpMethod.Post, "/api/v1/users/me/teacher-profile",
            new { schoolId = Guid.NewGuid(), invitationId = Guid.NewGuid() }, Guid.NewGuid());
        Assert.That(response.StatusCode, Is.EqualTo(failure == "expired" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Forbidden));
        await using var db = env.AdminDb();
        Assert.That(await db.Teachers.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task MissingInvitationIsInvalid()
    {
        await CreateUser();
        using var response = await env.SendAsync(HttpMethod.Post, "/api/v1/users/me/teacher-profile", new { }, Guid.NewGuid());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("identityId")]
    [TestCase("status")]
    [TestCase("userId")]
    public async Task RejectsOverposting(string field)
    {
        var body = new Dictionary<string, object?> { ["firstName"] = "Test", ["lastName"] = "User",
            ["emailAddress"] = null, ["phoneNumber"] = null, [field] = Guid.NewGuid().ToString() };
        using var response = await env.SendAsync(HttpMethod.Post, "/api/v1/users/me", body, Guid.NewGuid());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task RequiresOperationIdAndValidRevision()
    {
        using var missing = await env.SendAsync(HttpMethod.Post, "/api/v1/users/me", Update());
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await CreateUser();
        using var stale = await env.SendAsync(HttpMethod.Put, "/api/v1/users/me", Update(9), Guid.NewGuid());
        Assert.That(stale.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(stale.Headers.GetValues("X-Correlation-ID").Single(), Is.EqualTo("integration-test"));
    }

    [TestCase("wrong-audience")]
    [TestCase("expired")]
    [TestCase("malformed")]
    public async Task RejectsInvalidUserTokens(string failure)
    {
        var token = failure switch
        {
            "wrong-audience" => env.Dependencies.Token(env.Identity.ToString(), "other-service"),
            "expired" => env.Dependencies.Token(env.Identity.ToString(), expires: DateTime.UtcNow.AddMinutes(-1)),
            _ => "not-a-token"
        };
        using var response = await env.SendAsync(HttpMethod.Get, "/api/v1/users/me", token: token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain(token));
    }

    [Test]
    public async Task CurrentSessionAndDependencyFailuresFailClosed()
    {
        await CreateUser();
        env.Dependencies.SessionActive = false;
        using var inactive = await env.SendAsync(HttpMethod.Get, "/api/v1/users/me");
        Assert.That(inactive.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        env.Dependencies.Unavailable = true;
        using var down = await env.SendAsync(HttpMethod.Put, "/api/v1/users/me", Update(), Guid.NewGuid());
        Assert.That(down.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        await using var db = env.AdminDb();
        Assert.That((await db.Users.SingleAsync()).Revision, Is.EqualTo(1));
    }

    [Test]
    public async Task WorkloadAndScopeAreBothRequiredForContacts()
    {
        var userId = await CreateUser();
        var path = $"/internal/v1/users/{userId}/contact";
        using var user = await env.SendAsync(HttpMethod.Get, path);
        Assert.That(user.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var scopeMissing = await env.SendAsync(HttpMethod.Get, path, token: env.Workload("notifications", ""));
        Assert.That(scopeMissing.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var wrongClient = await env.SendAsync(HttpMethod.Get, path, token: env.Workload("school", "profiles.contact.read"));
        Assert.That(wrongClient.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var allowed = await env.SendAsync(HttpMethod.Get, path, token: env.Workload("notifications", "profiles.contact.read"));
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await allowed.Content.ReadAsStringAsync());
        Assert.That(json.RootElement.GetProperty("email").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task InternalStatusAndRoleBindingExposeOnlyDocumentedProjection()
    {
        var userId = await CreateUser();
        using var created = await env.SendAsync(HttpMethod.Post, "/api/v1/users/me/student-profile", new { }, Guid.NewGuid());
        using var receipt = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var roleId = receipt.RootElement.GetProperty("targetId").GetGuid();
        var workload = env.Workload("school", "profiles.status.read profiles.binding.read");
        using var status = await env.SendAsync(HttpMethod.Get, $"/internal/v1/users/{userId}/status", token: workload);
        using var binding = await env.SendAsync(HttpMethod.Get, $"/internal/v1/profiles/student/{roleId}", token: workload);
        Assert.That(status.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(binding.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var result = JsonDocument.Parse(await binding.Content.ReadAsStringAsync());
        Assert.That(result.RootElement.GetProperty("userId").GetGuid(), Is.EqualTo(userId));
        Assert.That(result.RootElement.GetProperty("active").GetBoolean(), Is.True);
        Assert.That(result.RootElement.TryGetProperty("emailAddress", out _), Is.False);
    }

    [Test]
    public async Task CompletedRetryStillRequiresCurrentSessionAdmission()
    {
        var operation = Guid.NewGuid();
        using var first = await env.CreateAsync(operation);
        env.Dependencies.SessionActive = false;
        using var retry = await env.CreateAsync(operation);
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task OperationKeyCannotExposeAnotherUsersReceipt()
    {
        var operation = Guid.NewGuid();
        using var first = await env.CreateAsync(operation);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var targetId = firstJson.RootElement.GetProperty("targetId").GetString();
        using var other = await env.CreateAsync(operation, env.Dependencies.Token(Guid.NewGuid().ToString()));
        Assert.That(other.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await other.Content.ReadAsStringAsync(), Does.Not.Contain(targetId));
    }

    [Test]
    public async Task OutboxFailureSchedulesRetryWithoutChangingPayload()
    {
        await using var db = env.AdminDb();
        var id = Guid.NewGuid();
        db.Outbox.Add(new() { EventId = id, AggregateId = Guid.NewGuid(), AggregateVersion = 2, EventType = "UserDeactivated",
            Body = "{\"test\":true}", CorrelationId = "test", OccurredAt = DateTimeOffset.UtcNow, NextAttemptAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var store = new OutboxStore(db, TimeProvider.System);
        Assert.That((await store.GetBacklogAsync(default)).Pending, Is.EqualTo(1));
        var first = await store.ClaimAsync(TimeSpan.FromMinutes(1), default);
        await store.FailAsync(id, first!.LeaseId!.Value, TimeSpan.FromMinutes(1), "PUBLISH_FAILED", default);
        Assert.That(await store.ClaimAsync(TimeSpan.FromMinutes(1), default), Is.Null);
        var retained = await db.Outbox.AsNoTracking().SingleAsync();
        Assert.That(retained.PublishedAt, Is.Null);
        Assert.That(retained.LastErrorCode, Is.EqualTo("PUBLISH_FAILED"));
        Assert.That(JsonDocument.Parse(retained.Body).RootElement.GetProperty("test").GetBoolean(), Is.True);
    }

    [Test]
    public async Task DeactivationRequiresOperatorAndAtomicallyCreatesOneOutboxEvent()
    {
        var userId = await CreateUser();
        var workload = env.Workload("operators", "profiles.deactivate");
        var path = $"/internal/v1/users/{userId}/deactivate";
        using var forbidden = await env.SendAsync(HttpMethod.Post, path, new { expectedRevision = 1 }, Guid.NewGuid(), workload, env.Token);
        Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var opToken = env.Dependencies.Token(TestEnvironment.OperatorIdentity.ToString());
        await CreateUser(opToken);
        var operation = Guid.NewGuid();
        using var result = await env.SendAsync(HttpMethod.Post, path, new { expectedRevision = 1 }, operation, workload, opToken);
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK), await result.Content.ReadAsStringAsync());
        using var repeat = await env.SendAsync(HttpMethod.Post, path, new { expectedRevision = 1 }, operation, workload, opToken);
        Assert.That(repeat.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var current = await env.SendAsync(HttpMethod.Get, "/api/v1/users/me");
        Assert.That(current.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        await using var db = env.AdminDb();
        Assert.That(await db.Outbox.CountAsync(), Is.EqualTo(1));
        var message = await db.Outbox.SingleAsync();
        using var json = JsonDocument.Parse(message.Body);
        Assert.That(json.RootElement.GetProperty("headers").GetProperty("eventType").GetString(), Is.EqualTo("UserDeactivated"));
        Assert.That(json.RootElement.GetProperty("payload").TryGetProperty("schoolId", out _), Is.False);
    }

    [Test]
    public async Task BootstrapRequiresBothWorkloadAndActivePlatformOperator()
    {
        var target = await CreateUser();
        var token = env.Dependencies.Token(TestEnvironment.OperatorIdentity.ToString());
        await CreateUser(token);
        using var response = await env.SendAsync(HttpMethod.Post, $"/internal/v1/users/{target}/administrator-profile/bootstrap",
            new { schoolId = Guid.NewGuid() }, Guid.NewGuid(), env.Workload("operators", "profiles.bootstrap"), token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        await using var db = env.AdminDb();
        Assert.That(await db.Administrators.CountAsync(x => x.UserId == target), Is.EqualTo(1));
    }

    [TestCase("absent")]
    [TestCase("expired")]
    [TestCase("denied")]
    [TestCase("wrong-fingerprint")]
    public async Task McpWritesRequireExactLiveConfirmation(string failure)
    {
        await CreateUser();
        env.Dependencies.ExpiredConfirmation = failure == "expired";
        env.Dependencies.ConfirmationAllowed = failure != "denied";
        env.Dependencies.WrongConfirmationFingerprint = failure == "wrong-fingerprint";
        using var response = await env.SendAsync(HttpMethod.Put, "/internal/v1/delegated/users/me",
            new { firstName = "Changed", lastName = "User", emailAddress = (string?)null, phoneNumber = (string?)null,
                expectedRevision = 1, confirmationId = failure == "absent" ? (Guid?)null : Guid.NewGuid() },
            Guid.NewGuid(), env.Workload("mcp", "profiles.mcp.write"), env.Token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        await using var db = env.AdminDb();
        Assert.That((await db.Users.SingleAsync()).Revision, Is.EqualTo(1));
    }

    [Test]
    public async Task McpConfirmationIsAuditedAndCompletedRetryDoesNotNeedFreshConsent()
    {
        await CreateUser();
        var workload = env.Workload("mcp", "profiles.mcp.write");
        var operation = Guid.NewGuid();
        var confirmationId = Guid.NewGuid();
        var body = new { firstName = "Changed", lastName = "User", emailAddress = (string?)null,
            phoneNumber = (string?)null, expectedRevision = 1, confirmationId };
        using var result = await env.SendAsync(HttpMethod.Put, "/internal/v1/delegated/users/me", body, operation, workload, env.Token);
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK), await result.Content.ReadAsStringAsync());
        env.Dependencies.ExpiredConfirmation = true;
        using var retry = await env.SendAsync(HttpMethod.Put, "/internal/v1/delegated/users/me", body, operation, workload, env.Token);
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await using var db = env.AdminDb();
        var op = await db.Operations.SingleAsync(x => x.OperationId == operation);
        Assert.That(op.Channel, Is.EqualTo(OperationChannel.MCP));
        Assert.That(op.ConfirmationId, Is.EqualTo(confirmationId));
    }

    [Test]
    public async Task DelegatedRouteCannotUseWorkloadAsUser()
    {
        using var response = await env.SendAsync(HttpMethod.Get, "/internal/v1/delegated/users/me",
            token: env.Workload("mcp", "profiles.mcp.write"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task FailedAuditInsertRollsBackProfileAndOutbox()
    {
        var target = await CreateUser();
        var token = env.Dependencies.Token(TestEnvironment.OperatorIdentity.ToString());
        await CreateUser(token);
        await using var db = env.AdminDb();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_reject_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'test-only'; END $$;
            CREATE TRIGGER test_reject BEFORE INSERT ON profile_operation FOR EACH ROW EXECUTE FUNCTION test_reject_audit();
            """);
        try
        {
            using var response = await env.SendAsync(HttpMethod.Post, $"/internal/v1/users/{target}/deactivate",
                new { expectedRevision = 1 }, Guid.NewGuid(), env.Workload("operators", "profiles.deactivate"), token);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That((await db.Users.SingleAsync(x => x.UserId == target)).Status, Is.EqualTo(UserStatus.Active));
            Assert.That(await db.Outbox.CountAsync(), Is.Zero);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_reject ON profile_operation; DROP FUNCTION test_reject_audit()"); }
    }

    [Test]
    public async Task OutboxLeasesRecoverAndStaleWorkerCannotAcknowledgeNewLease()
    {
        await using var db = env.AdminDb();
        var id = Guid.NewGuid();
        db.Outbox.Add(new() { EventId = id, AggregateId = Guid.NewGuid(), AggregateVersion = 1, EventType = "UserDeactivated",
            Body = "{}", CorrelationId = "test", OccurredAt = DateTimeOffset.UtcNow, NextAttemptAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var store = new OutboxStore(db, TimeProvider.System);
        var first = await store.ClaimAsync(TimeSpan.Zero, default);
        var second = await store.ClaimAsync(TimeSpan.FromMinutes(1), default);
        Assert.That(second!.EventId, Is.EqualTo(id));
        Assert.That(second.LeaseId, Is.Not.EqualTo(first!.LeaseId));
        await store.CompleteAsync(id, first.LeaseId!.Value, default);
        Assert.That((await db.Outbox.AsNoTracking().SingleAsync()).PublishedAt, Is.Null);
        await store.CompleteAsync(id, second.LeaseId!.Value, default);
        Assert.That((await db.Outbox.AsNoTracking().SingleAsync()).PublishedAt, Is.Not.Null);
    }

    [Test]
    public async Task RuntimeCannotRewriteAuditOrDeleteProfiles()
    {
        await CreateUser();
        using var scope = env.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VairasDbContext>();
        var audit = Assert.ThrowsAsync<PostgresException>(async () => await db.Database.ExecuteSqlRawAsync("UPDATE profile_operation SET outcome_code='tampered'"));
        Assert.That(audit!.SqlState, Is.EqualTo(PostgresErrorCodes.InsufficientPrivilege));
        var delete = Assert.ThrowsAsync<PostgresException>(async () => await db.Database.ExecuteSqlRawAsync("DELETE FROM user_profile"));
        Assert.That(delete!.SqlState, Is.EqualTo(PostgresErrorCodes.InsufficientPrivilege));
    }

    [Test]
    public async Task HealthAndOpenApiAreAvailable()
    {
        Assert.That((await env.Client.GetAsync("/health/live")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await env.Client.GetAsync("/health/ready")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var response = await env.Client.GetAsync("/openapi/v1.json");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var text = await response.Content.ReadAsStringAsync();
        Assert.That(text, Does.Contain("/api/v1/users/me").And.Contain("/internal/v1/users/{userId}/status"));
    }

    [Test]
    public async Task MissingDatabasePermissionFailsReadinessButNotLiveness()
    {
        await using var db = env.AdminDb();
        await env.Database.SetOutboxReadAsync(false);
        try
        {
            Assert.That((await env.Client.GetAsync("/health/ready")).StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That((await env.Client.GetAsync("/health/live")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        finally { await env.Database.SetOutboxReadAsync(true); }
    }
}
