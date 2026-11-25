using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public sealed record SchoolAuthorityResult(bool Allowed, Guid ActorUserId, Guid TargetUserId, Guid SchoolId,
    Guid? InvitationId, ProfileKind Role, bool Bootstrap, string AuthorityBasis, DateTimeOffset ValidUntil,
    Guid? IssuerUserId = null);
