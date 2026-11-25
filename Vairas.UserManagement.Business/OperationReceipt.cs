using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

/// <summary>Stable command result; the current resource is fetched separately.</summary>
public sealed record OperationReceipt(Guid OperationId, OperationOutcome Outcome, string Code,
    string TargetKind, Guid TargetId, long? ResultingRevision, DateTimeOffset RecordedAt);
