using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Business;
namespace Vairas.UserManagement.Controllers;
[Route("internal/v1/users/{userId:guid}/document-profile"), Authorize(Policy = "profiles.documents.read")]
public sealed class DocumentProfileController(IProfilesService profiles) : BaseApiController
{
    /// <summary>Read current name and profile revision without contacts or credentials.</summary>
    [HttpGet(Name = "getDocumentProfile")]
    public async Task<ActionResult<DocumentProfileResponse>> Read(Guid userId, CancellationToken ct)
    { var p = await profiles.FindUserAsync(userId,ct) ?? throw ServiceErrors.Missing(); return Ok(new DocumentProfileResponse(p.UserId,p.FirstName,p.LastName,p.Revision)); }
}
