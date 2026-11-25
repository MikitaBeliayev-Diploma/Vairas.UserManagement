using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
namespace Vairas.UserManagement.Controllers;
[Route("internal/v1/profiles/assistant-actor"), Authorize(Policy = "profiles.binding.read")]
public sealed class AssistantActorController(IProfilesService profiles) : BaseApiController
{
    [HttpPost(Name = "readAssistantActorBinding")]
    public async Task<ActionResult<AssistantActorResponse>> Read(AssistantActorRequest request, CancellationToken ct)
    {
        if (User.FindFirst("sub")?.Value is not ("school-management" or "scheduling" or "learning-tracking")) throw ServiceErrors.Forbidden();
        if (request.IdentityId == Guid.Empty || request.SessionId == Guid.Empty) throw ServiceErrors.Forbidden();
        var profile = await profiles.GetMyAsync(new(request.IdentityId, request.SessionId, null, OperationChannel.MCP, HttpContext.TraceIdentifier, null), ct);
        return Ok(new AssistantActorResponse(profile.UserId, profile.Status.ToString(), profile.StudentId, profile.TeacherId, profile.AdministratorId));
    }
}
