namespace Vairas.UserManagement.Controllers;

/// <summary>Authoritative global operational status.</summary>
public sealed record UserStatusResponse(Guid UserId, bool Active);
