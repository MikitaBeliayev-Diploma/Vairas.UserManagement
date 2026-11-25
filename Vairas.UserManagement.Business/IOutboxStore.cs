using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public interface IOutboxStore
{
    Task<OutboxBacklog> GetBacklogAsync(CancellationToken ct);
    Task<OutboxMessage?> ClaimAsync(TimeSpan lease, CancellationToken ct);
    Task CompleteAsync(Guid eventId, Guid leaseId, CancellationToken ct);
    Task FailAsync(Guid eventId, Guid leaseId, TimeSpan delay, string errorCode, CancellationToken ct);
}
