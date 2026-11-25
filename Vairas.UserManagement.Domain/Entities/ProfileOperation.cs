namespace Vairas.UserManagement.Domain.Entities;

public sealed class ProfileOperation
{
    public Guid OperationId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorService { get; init; }
    public Guid? RequestingSchoolId { get; init; }
    public required string AuthorityBasis { get; init; }
    public OperationChannel Channel { get; init; }
    public required string CommandName { get; init; }
    public required string TargetKind { get; init; }
    public Guid TargetId { get; init; }
    public long? ExpectedRevision { get; init; }
    public long? ResultingRevision { get; init; }
    public required string RequestFingerprint { get; init; }
    public OperationOutcome Outcome { get; init; }
    public required string OutcomeCode { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public string? Reason { get; init; }
    public Guid? ConfirmationId { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
}
