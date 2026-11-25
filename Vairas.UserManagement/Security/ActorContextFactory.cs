using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
using Vairas.UserManagement.Middleware;

namespace Vairas.UserManagement.Security;

public sealed class ActorContextFactory : IActorContextFactory
{
    public async Task<ActorContext> CreateAsync(HttpContext http, CancellationToken ct)
    {
        var user = await http.AuthenticateAsync("User");
        if (!user.Succeeded || user.Principal is null || !Guid.TryParse(user.Principal.FindFirstValue("sub"), out var identityId) ||
            identityId == Guid.Empty || !Guid.TryParse(user.Principal.FindFirstValue("sid"), out var sessionId) || sessionId == Guid.Empty)
            throw new UnauthorizedException("INVALID_IDENTITY", "A valid user token with a session is required.");
        var channel = OperationChannel.Web;
        string? workload = null;
        if (http.Request.Path.StartsWithSegments("/internal"))
        {
            var service = await http.AuthenticateAsync("Workload");
            if (!service.Succeeded) throw ServiceErrors.Forbidden();
            workload = service.Principal!.FindFirstValue("sub") ?? throw ServiceErrors.Forbidden();
            channel = http.Request.Path.StartsWithSegments("/internal/v1/delegated") ? OperationChannel.MCP : OperationChannel.Internal;
        }
        return new(identityId, sessionId, workload, channel, CorrelationMiddleware.GetId(http), Activity.Current?.Id);
    }
}
