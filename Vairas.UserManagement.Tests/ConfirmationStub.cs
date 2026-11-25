using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

internal sealed class ConfirmationStub(TestClock clock) : IConfirmationClient
{
    public int Calls;
    public Func<ConfirmationResult, ConfirmationResult>? Reply;
    public Task<ConfirmationResult> ValidateAsync(ConfirmationRequest request, CancellationToken ct)
    {
        Calls++;
        var response = new ConfirmationResult(true, request.ConfirmationId, request.IdentityId, request.OperationId,
            request.RequestFingerprint, request.SchoolId, request.CommandName, clock.Now.AddSeconds(-5), clock.Now.AddMinutes(1));
        return Task.FromResult(Reply?.Invoke(response) ?? response);
    }
}
