using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public sealed record SchoolAuthorityRequest(Guid ActorUserId, Guid TargetUserId, Guid SchoolId,
    Guid? InvitationId, ProfileKind Role, bool Bootstrap);
