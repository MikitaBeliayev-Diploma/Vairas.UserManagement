namespace Vairas.UserManagement.Controllers;

/// <summary>Minimal contact details; a contact address can be absent.</summary>
public sealed record UserContactResponse(Guid UserId, string? Email);
