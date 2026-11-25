using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
using Vairas.UserManagement.Security;
using Vairas.UserManagement.Middleware;

namespace Vairas.UserManagement.Controllers;

[Route("api/v1/users/me")]
[Route("internal/v1/delegated/users/me")]
[Authorize(Policy = "ProfileEntry")]
public sealed class ProfilesController(IProfilesService profiles, IActorContextFactory actors) : BaseApiController
{
    /// <summary>Read the current user's global profile and role bindings.</summary>
    [HttpGet]
    [ProducesResponseType<UserProfileResult>(200)]
    public async Task<ActionResult<UserProfileResult>> Get(CancellationToken ct) =>
        Ok(await profiles.GetMyAsync(await actors.CreateAsync(HttpContext, ct), ct));

    /// <summary>Create a profile bound to the authenticated identity.</summary>
    [HttpPost]
    [ProducesResponseType<OperationReceipt>(201)]
    public Task<IActionResult> Create(ProfileRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operationId, CancellationToken ct) =>
        Execute(new() { OperationId = operationId, Kind = ProfileCommandKind.CreateUser,
            FirstName = request.FirstName, LastName = request.LastName, EmailAddress = request.EmailAddress,
            PhoneNumber = request.PhoneNumber, ExpectedRevision = request.ExpectedRevision,
            ConfirmationId = request.ConfirmationId }, true, Request.Path, ct);

    /// <summary>Replace personal information using the expected resource revision.</summary>
    [HttpPut]
    [ProducesResponseType<OperationReceipt>(200)]
    public Task<IActionResult> Update(ProfileRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operationId, CancellationToken ct) =>
        Execute(new() { OperationId = operationId, Kind = ProfileCommandKind.UpdateUser,
            FirstName = request.FirstName, LastName = request.LastName, EmailAddress = request.EmailAddress,
            PhoneNumber = request.PhoneNumber, ExpectedRevision = request.ExpectedRevision,
            ConfirmationId = request.ConfirmationId }, false, Request.Path, ct);

    /// <summary>Read a role profile; it does not establish school membership.</summary>
    [HttpGet("{role}-profile")]
    [ProducesResponseType<RoleProfileResult>(200)]
    public async Task<ActionResult<RoleProfileResult>> GetRole(string role, CancellationToken ct) =>
        Ok(await profiles.GetMyRoleAsync(ParseKind(role), await actors.CreateAsync(HttpContext, ct), ct));

    /// <summary>Create or reuse a stable role profile after current authorization checks.</summary>
    [HttpPost("{role}-profile")]
    [ProducesResponseType<OperationReceipt>(201)]
    [ProducesResponseType<OperationReceipt>(200)]
    public Task<IActionResult> CreateRole(string role, RoleRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid operationId, CancellationToken ct)
    {
        var kind = ParseKind(role) switch { ProfileKind.Student => ProfileCommandKind.CreateStudent,
            ProfileKind.Teacher => ProfileCommandKind.CreateTeacher, _ => ProfileCommandKind.CreateAdministrator };
        return Execute(new() { OperationId = operationId, Kind = kind, SchoolId = request.SchoolId,
            InvitationId = request.InvitationId, ConfirmationId = request.ConfirmationId }, true, Request.Path, ct);
    }

    private async Task<IActionResult> Execute(ProfileCommand command, bool create, string location, CancellationToken ct)
    {
        var receipt = await profiles.ExecuteAsync(command, await actors.CreateAsync(HttpContext, ct), ct);
        return CommandResult(this, receipt, create, location);
    }

    internal static IActionResult CommandResult(ControllerBase controller, OperationReceipt result, bool create, string location)
    {
        if (result.Outcome == OperationOutcome.Rejected)
        {
            var status = result.Code switch { "NOT_FOUND" => 404, "REVISION_CONFLICT" or "PROFILE_EXISTS" => 409, _ => 422 };
            return controller.Problem(statusCode: status, title: "The command was rejected.",
                extensions: new Dictionary<string, object?> { ["code"] = result.Code,
                    ["correlationId"] = CorrelationMiddleware.GetId(controller.HttpContext), ["operation"] = result });
        }
        return create && result.Code != "ALREADY_EXISTS" ? controller.Created(location, result) : controller.Ok(result);
    }

    internal static ProfileKind ParseKind(string role) => role.ToLowerInvariant() switch
    {
        "student" => ProfileKind.Student, "teacher" => ProfileKind.Teacher, "administrator" => ProfileKind.Administrator,
        _ => throw ServiceErrors.Missing()
    };
}
