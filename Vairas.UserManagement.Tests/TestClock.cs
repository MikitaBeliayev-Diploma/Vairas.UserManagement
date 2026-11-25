using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
