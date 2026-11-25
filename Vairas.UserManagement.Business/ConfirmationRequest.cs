using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public sealed record ConfirmationRequest(Guid ConfirmationId, Guid IdentityId, Guid OperationId,
    string RequestFingerprint, Guid? SchoolId, string CommandName);
