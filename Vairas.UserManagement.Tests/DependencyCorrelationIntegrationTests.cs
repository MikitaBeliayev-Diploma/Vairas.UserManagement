using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Vairas.UserManagement.Integrations;
using Vairas.UserManagement.Middleware;

namespace Vairas.UserManagement.Tests;

[TestFixture, NonParallelizable]
public sealed class DependencyCorrelationIntegrationTests
{
    private readonly TestEnvironment env = new();
    [OneTimeSetUp] public Task Start() => env.StartAsync();
    [OneTimeTearDown] public async Task Stop() => await env.DisposeAsync();

    [Test]
    public async Task ApiPropagatesNormalizedCorrelationThroughTokenAuthSchoolAndConfirmationClients()
    {
        await env.ResetAsync();
        using (var create = await Send(HttpMethod.Post, "/api/v1/users/me", "create-request", new
        { firstName = "Synthetic", lastName = "User", emailAddress = (string?)null, phoneNumber = (string?)null }))
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created), await create.Content.ReadAsStringAsync());
        var first = env.Dependencies.DependencyCorrelations.ToArray();
        Assert.That(first.Any(x => x.Path == "/connect/token"), Is.True);
        Assert.That(first.Any(x => x.Path == "/internal/v1/sessions/validate"), Is.True);
        Assert.That(first.All(x => x.CorrelationId == "create-request"), Is.True);

        env.Dependencies.DependencyCorrelations.Clear();
        using (var teacher = await Send(HttpMethod.Post, "/api/v1/users/me/teacher-profile", "school-request",
            new { schoolId = Guid.NewGuid(), invitationId = Guid.NewGuid() }))
            Assert.That(teacher.StatusCode, Is.EqualTo(HttpStatusCode.Created), await teacher.Content.ReadAsStringAsync());
        var school = env.Dependencies.DependencyCorrelations.ToArray();
        Assert.That(school.Any(x => x.Path == "/internal/v1/profile-provisioning/validate"), Is.True);
        Assert.That(school.All(x => x.CorrelationId == "school-request"), Is.True);

        env.Dependencies.DependencyCorrelations.Clear();
        using var update = await Send(HttpMethod.Put, "/internal/v1/delegated/users/me", "invalid id!", new
        {
            firstName = "Updated", lastName = "User", emailAddress = (string?)null,
            phoneNumber = (string?)null, expectedRevision = 1, confirmationId = Guid.NewGuid()
        }, env.Workload("mcp", "profiles.mcp.write"), env.Token);
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK), await update.Content.ReadAsStringAsync());
        var normalized = update.Headers.GetValues("X-Correlation-ID").Single();
        Assert.That(Guid.TryParseExact(normalized, "N", out _), Is.True);
        var consent = env.Dependencies.DependencyCorrelations.ToArray();
        Assert.That(consent.Any(x => x.Path == "/internal/v1/confirmations/validate"), Is.True);
        Assert.That(consent.All(x => x.CorrelationId == normalized), Is.True);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string correlation, object body,
        string? token = null, string? delegated = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? env.Token);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlation);
        if (delegated is not null) request.Headers.Add("X-User-Authorization", "Bearer " + delegated);
        return await env.Client.SendAsync(request);
    }
}
