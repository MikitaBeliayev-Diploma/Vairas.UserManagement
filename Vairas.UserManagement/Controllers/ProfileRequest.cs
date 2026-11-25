namespace Vairas.UserManagement.Controllers;

/// <summary>Personal information for creating or replacing the authenticated profile.</summary>
public sealed record ProfileRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string? EmailAddress { get; init; }
    public required string? PhoneNumber { get; init; }
    public long? ExpectedRevision { get; init; }
    public Guid? ConfirmationId { get; init; }
}
