namespace Vairas.UserManagement.Domain.Entities;

public sealed class AdministratorProfile
{
    public Guid AdministratorId { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
