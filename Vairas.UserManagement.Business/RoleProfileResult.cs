using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

/// <summary>Minimal role profile binding; existence does not grant school permissions.</summary>
public sealed record RoleProfileResult(ProfileKind Kind, Guid ProfileId, Guid UserId, bool Active);
