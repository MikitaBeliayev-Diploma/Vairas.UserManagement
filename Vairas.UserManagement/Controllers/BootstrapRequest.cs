namespace Vairas.UserManagement.Controllers;

/// <summary>School-authorized bootstrap of the first administrator.</summary>
public sealed record BootstrapRequest
{
    public required Guid SchoolId { get; init; }
}
