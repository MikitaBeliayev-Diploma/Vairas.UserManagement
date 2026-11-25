using Vairas.UserManagement.Business;

namespace Vairas.UserManagement.Security;

public interface IActorContextFactory
{
    Task<ActorContext> CreateAsync(HttpContext http, CancellationToken ct);
}
