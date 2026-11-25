namespace Vairas.UserManagement.Controllers;

/// <summary>Global deactivation of a user at a specific revision.</summary>
public sealed record DeactivateRequest
{
    public required long ExpectedRevision { get; init; }
    public string? Reason { get; init; }
}
