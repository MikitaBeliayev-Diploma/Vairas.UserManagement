using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

internal sealed class SessionStub : IAuthSessionValidator
{
    public DateTimeOffset ValidUntil;
    public Exception? Error;
    public Task<DateTimeOffset> ValidateAsync(ActorContext actor, CancellationToken ct) =>
        Error is null ? Task.FromResult(ValidUntil) : Task.FromException<DateTimeOffset>(Error);
}
