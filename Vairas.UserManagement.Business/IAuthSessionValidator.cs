using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public interface IAuthSessionValidator
{
    Task<DateTimeOffset> ValidateAsync(ActorContext actor, CancellationToken ct);
}
