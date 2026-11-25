using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public interface IEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken ct);
}
