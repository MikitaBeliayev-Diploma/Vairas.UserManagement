namespace Vairas.UserManagement.Domain.Entities;

public sealed class StudentProfile
{
    public Guid StudentId { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
