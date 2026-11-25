using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

/// <summary>Validated arguments for an application command. Actor and channel are server-derived.</summary>
public sealed record ProfileCommand
{
    public required Guid OperationId { get; init; }
    public required ProfileCommandKind Kind { get; init; }
    public Guid? TargetUserId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? EmailAddress { get; init; }
    public string? PhoneNumber { get; init; }
    public long? ExpectedRevision { get; init; }
    public Guid? SchoolId { get; init; }
    public Guid? InvitationId { get; init; }
    public Guid? ConfirmationId { get; init; }
    public string? Reason { get; init; }
}
