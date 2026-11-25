using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public sealed record OutboxBacklog(long Pending, DateTimeOffset? OldestAt);
