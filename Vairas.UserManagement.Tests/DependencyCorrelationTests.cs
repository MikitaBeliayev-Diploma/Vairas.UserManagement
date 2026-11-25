using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Vairas.UserManagement.Integrations;
using Vairas.UserManagement.Middleware;

namespace Vairas.UserManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class DependencyCorrelationTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public ConcurrentDictionary<string, string> Captured { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Captured[request.RequestUri!.AbsolutePath] = request.Headers.TryGetValues("X-Correlation-ID", out var values)
                ? values.Single() : "";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Test]
    public async Task PooledHandlerReadsEachRequestContextAndDoesNotLeakToBackgroundRequests()
    {
        var contexts = new HttpContextAccessor();
        var capture = new CaptureHandler();
        using var client = new HttpClient(new DependencyCorrelationHandler(contexts) { InnerHandler = capture });
        foreach (var id in new[] { "request.one", "request.two" })
        {
            contexts.HttpContext = new DefaultHttpContext();
            contexts.HttpContext.Items[CorrelationMiddleware.ItemKey] = id;
            using var response = await client.GetAsync("https://dependency/" + id);
            Assert.That(capture.Captured["/" + id], Is.EqualTo(id));
        }
        contexts.HttpContext = null;
        using var background = await client.GetAsync("https://dependency/background");
        Assert.That(capture.Captured["/background"], Is.Empty);
        Assert.That(client.DefaultRequestHeaders.Contains("X-Correlation-ID"), Is.False);
    }

    [Test]
    public async Task ConcurrentRequestsOnOneHandlerKeepTheirOwnCorrelationIds()
    {
        var contexts = new HttpContextAccessor();
        var capture = new CaptureHandler();
        using var client = new HttpClient(new DependencyCorrelationHandler(contexts) { InnerHandler = capture });
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Send(string id)
        {
            contexts.HttpContext = new DefaultHttpContext();
            contexts.HttpContext.Items[CorrelationMiddleware.ItemKey] = id;
            await start.Task;
            using var response = await client.GetAsync("https://dependency/" + id);
        }
        var ids = Enumerable.Range(0, 20).Select(i => "parallel-" + i).ToArray();
        var requests = ids.Select(Send).ToArray();
        start.SetResult();
        await Task.WhenAll(requests);
        Assert.That(capture.Captured.Count, Is.EqualTo(ids.Length));
        foreach (var id in ids) Assert.That(capture.Captured["/" + id], Is.EqualTo(id));
    }

    [TestCase("valid.id_123-456", true)]
    [TestCase("invalid id!", false)]
    [TestCase("", false)]
    public async Task SendsMiddlewareNormalizedIdRatherThanRawInboundOrStaleOutgoingHeader(string inbound, bool retained)
    {
        var contexts = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        contexts.HttpContext.Request.Headers["X-Correlation-ID"] = inbound;
        var capture = new CaptureHandler();
        using var client = new HttpClient(new DependencyCorrelationHandler(contexts) { InnerHandler = capture });
        var middleware = new CorrelationMiddleware(async _ =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://dependency/normalized");
            request.Headers.Add("X-Correlation-ID", "stale-value");
            using var response = await client.SendAsync(request);
        });
        await middleware.InvokeAsync(contexts.HttpContext);
        var normalized = contexts.HttpContext.Response.Headers["X-Correlation-ID"].ToString();
        Assert.That(capture.Captured["/normalized"], Is.EqualTo(normalized));
        if (retained) Assert.That(normalized, Is.EqualTo(inbound));
        else Assert.That(Guid.TryParseExact(normalized, "N", out _), Is.True);
        contexts.HttpContext = null;
    }
}
