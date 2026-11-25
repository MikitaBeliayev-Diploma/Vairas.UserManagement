using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Integrations;

public sealed class AuthSessionClient(DependencyTransport transport, IOptions<DependencyOptions> options,
    TimeProvider clock) : IAuthSessionValidator
{
    public async Task<DateTimeOffset> ValidateAsync(ActorContext actor, CancellationToken ct)
    {
        var result = await transport.PostAsync<SessionValidationRequest, SessionValidationResult>(options.Value.AuthBaseUrl,
            "internal/v1/sessions/validate", new(actor.IdentityId, actor.SessionId), ct);
        if (result.ValidUntil <= clock.GetUtcNow()) throw ServiceErrors.Dependency();
        if (!result.Active || result.IdentityId != actor.IdentityId || result.SessionId != actor.SessionId)
            throw new UnauthorizedException("SESSION_INVALID", "The session is not active.");
        // A lock wait may not extend an old admission decision indefinitely.
        return result.ValidUntil < clock.GetUtcNow().AddSeconds(30) ? result.ValidUntil : clock.GetUtcNow().AddSeconds(30);
    }
}
