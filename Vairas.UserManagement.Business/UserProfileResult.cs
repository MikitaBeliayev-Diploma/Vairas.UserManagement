using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

/// <summary>The authenticated user's global business profile.</summary>
public sealed record UserProfileResult(Guid UserId, string FirstName, string LastName, string? EmailAddress,
    string? PhoneNumber, UserStatus Status, long Revision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    Guid? StudentId, Guid? TeacherId, Guid? AdministratorId);
