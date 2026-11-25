using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public sealed record ConfirmationResult(bool Allowed, Guid ConfirmationId, Guid IdentityId, Guid OperationId,
    string RequestFingerprint, Guid? SchoolId, string CommandName, DateTimeOffset ConfirmedAt, DateTimeOffset ExpiresAt);
