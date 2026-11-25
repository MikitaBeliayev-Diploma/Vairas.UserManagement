using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

internal sealed class SchoolStub(TestClock clock) : ISchoolAuthorityClient
{
    public int Calls;
    public Func<SchoolAuthorityResult, SchoolAuthorityResult>? Reply;
    public Task<SchoolAuthorityResult> ValidateAsync(SchoolAuthorityRequest request, CancellationToken ct)
    {
        Calls++;
        var response = new SchoolAuthorityResult(true, request.ActorUserId, request.TargetUserId, request.SchoolId,
            request.InvitationId, request.Role, request.Bootstrap, "invitation:unit", clock.Now.AddMinutes(1), request.ActorUserId);
        return Task.FromResult(Reply?.Invoke(response) ?? response);
    }
}
