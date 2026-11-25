namespace Vairas.UserManagement.Domain.Entities;

public sealed class UserProfile
{
    public Guid UserId { get; init; }
    public Guid IdentityId { get; init; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
