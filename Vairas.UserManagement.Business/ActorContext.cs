using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public sealed record ActorContext(Guid IdentityId, Guid SessionId, string? Workload,
    OperationChannel Channel, string CorrelationId, string? TraceParent);
