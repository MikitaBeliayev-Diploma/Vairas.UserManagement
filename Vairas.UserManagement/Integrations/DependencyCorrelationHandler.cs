using Vairas.UserManagement.Middleware;
namespace Vairas.UserManagement.Integrations;

/// <summary>Propagates the normalized request ID at send time, including token acquisition.</summary>
public sealed class DependencyCorrelationHandler(IHttpContextAccessor contexts) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // HttpClientFactory pools handlers: never capture a request's context in a field
        // or modify shared DefaultRequestHeaders.
        request.Headers.Remove("X-Correlation-ID");
        if (contexts.HttpContext is { } context)
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", CorrelationMiddleware.GetId(context));
        return base.SendAsync(request, cancellationToken);
    }
}
