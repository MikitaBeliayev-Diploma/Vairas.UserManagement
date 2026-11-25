namespace Vairas.UserManagement.Controllers;

/// <summary>Role provisioning arguments; school and invitation are mandatory for staff roles.</summary>
public sealed record RoleRequest
{
    public Guid? SchoolId { get; init; }
    public Guid? InvitationId { get; init; }
    public Guid? ConfirmationId { get; init; }
}
