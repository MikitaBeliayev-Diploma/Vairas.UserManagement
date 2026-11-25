using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

/// <summary>Role identifiers bound to one global user, used by consumers reconciling a deactivation; no contact data.</summary>
public sealed record UserRoleProfilesResult(Guid UserId, UserStatus Status, long Revision, Guid? StudentId, Guid? TeacherId, Guid? AdministratorId);
