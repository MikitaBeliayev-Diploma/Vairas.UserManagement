using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Controllers;
[Route("internal/v1/users/{userId:guid}/notification-binding"), Authorize(Policy = "profiles.contact.read")]
public sealed class NotificationBindingController(IProfilesService profiles) : BaseApiController
{
    /// <summary>Resolve the canonical user's immutable Authentication binding and current activity.</summary>
    [HttpGet(Name = "getNotificationContactBinding")]
    public async Task<ActionResult<NotificationBindingResponse>> Read(Guid userId, CancellationToken ct)
    {
        var user = await profiles.FindUserAsync(userId, ct) ?? throw ServiceErrors.Missing();
        return Ok(new NotificationBindingResponse(user.UserId, user.IdentityId, user.Status == UserStatus.Active, user.Revision));
    }
}
