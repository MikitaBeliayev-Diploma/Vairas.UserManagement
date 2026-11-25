using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
using Vairas.UserManagement.Security;

namespace Vairas.UserManagement.Controllers;

[Route("internal/v1")]
public sealed class InternalProfilesController(IProfilesService profiles, IActorContextFactory actors) : BaseApiController
{
    /// <summary>Read operational status using a permitted workload identity.</summary>
    [HttpGet("users/{userId:guid}/status")]
    [Authorize(Policy = "profiles.status.read")]
    public async Task<ActionResult<UserStatusResponse>> Status(Guid userId, CancellationToken ct)
    {
        var user = await profiles.FindUserAsync(userId, ct) ?? throw ServiceErrors.Missing();
        return Ok(new UserStatusResponse(user.UserId, user.Status == UserStatus.Active));
    }

    /// <summary>Read a minimal contact projection for a configured notification/document workload.</summary>
    [HttpGet("users/{userId:guid}/contact")]
    [Authorize(Policy = "profiles.contact.read")]
    public async Task<ActionResult<UserContactResponse>> Contact(Guid userId, CancellationToken ct)
    {
        var user = await profiles.FindUserAsync(userId, ct) ?? throw ServiceErrors.Missing();
        return Ok(new UserContactResponse(user.UserId, user.EmailAddress));
    }

    /// <summary>Resolve a global user to its role identifiers and current status, for deactivation reconciliation.</summary>
    [HttpGet("users/{userId:guid}/role-profiles")]
    [Authorize(Policy = "profiles.binding.read")]
    public async Task<ActionResult<UserRoleProfilesResult>> RoleProfiles(Guid userId, CancellationToken ct) =>
        Ok(await profiles.FindRoleProfilesAsync(userId, ct));

    /// <summary>Resolve a role identifier to its global user and active state.</summary>
    [HttpGet("profiles/{kind}/{profileId:guid}")]
    [Authorize(Policy = "profiles.binding.read")]
    public async Task<ActionResult<RoleProfileResult>> Binding(string kind, Guid profileId, CancellationToken ct) =>
        Ok(await profiles.FindRoleAsync(ProfilesController.ParseKind(kind), profileId, ct));

    /// <summary>Deactivate a global user using both platform-operator and workload authorization.</summary>
    [HttpPost("users/{userId:guid}/deactivate")]
    [Authorize(Policy = "profiles.deactivate")]
    public async Task<IActionResult> Deactivate(Guid userId, DeactivateRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid operationId, CancellationToken ct)
    {
        var result = await profiles.ExecuteAsync(new() { OperationId = operationId, Kind = ProfileCommandKind.Deactivate,
            TargetUserId = userId, ExpectedRevision = request.ExpectedRevision, Reason = request.Reason },
            await actors.CreateAsync(HttpContext, ct), ct);
        return ProfilesController.CommandResult(this, result, false, "");
    }

    /// <summary>Bootstrap an administrator after checking platform and school authority.</summary>
    [HttpPost("users/{userId:guid}/administrator-profile/bootstrap")]
    [Authorize(Policy = "profiles.bootstrap")]
    public async Task<IActionResult> Bootstrap(Guid userId, BootstrapRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid operationId, CancellationToken ct)
    {
        var result = await profiles.ExecuteAsync(new() { OperationId = operationId, Kind = ProfileCommandKind.BootstrapAdministrator,
            TargetUserId = userId, SchoolId = request.SchoolId }, await actors.CreateAsync(HttpContext, ct), ct);
        return ProfilesController.CommandResult(this, result, true,
            $"/internal/v1/profiles/administrator/{result.TargetId:D}");
    }
}
