namespace Vairas.UserManagement.Domain.Entities;

public sealed class TeacherProfile
{
    public Guid TeacherId { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
