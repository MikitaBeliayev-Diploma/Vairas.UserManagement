using NUnit.Framework;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class SchoolAdmissionReplayTests
{
    [TestCase(ProfileCommandKind.CreateTeacher)]
    [TestCase(ProfileCommandKind.CreateAdministrator)]
    [TestCase(ProfileCommandKind.BootstrapAdministrator)]
    public async Task CompletedCommandCannotReplayWithExpiredSchoolAdmission(ProfileCommandKind kind)
    {
        var (f, command) = Fixture(kind);
        var receipt = await f.Execute(command);
        var commits = f.Store.Commits;
        f.Schools.Reply = reply => reply with { ValidUntil = f.Clock.Now };

        var error = Business.ThrowsBusinessAsync(() => f.Execute(command));

        Assert.Multiple(() => {
            Assert.That(error.HttpStatus(), Is.EqualTo(503));
            Assert.That(error.Code, Is.EqualTo("DEPENDENCY_UNAVAILABLE"));
            Assert.That(f.Store.Commits, Is.EqualTo(commits));
            Assert.That(f.Store.Operations.Single().OperationId, Is.EqualTo(receipt.OperationId));
        });
        f.Schools.Reply = null;
        Assert.That(await f.Execute(command), Is.EqualTo(receipt));
    }

    [Test]
    public async Task AdmissionExpiringWhileWaitingForTransactionCannotReplay()
    {
        var (f, command) = Fixture(ProfileCommandKind.CreateTeacher);
        var receipt = await f.Execute(command);
        f.Store.OnBegin = () => f.Clock.Now = f.Clock.Now.AddMinutes(1);

        var error = Business.ThrowsBusinessAsync(() => f.Execute(command));

        Assert.That(error.HttpStatus(), Is.EqualTo(503));
        Assert.That(f.Store.Operations.Single().OperationId, Is.EqualTo(receipt.OperationId));
        Assert.That(f.Store.Commits, Is.EqualTo(1));
    }

    [Test]
    public async Task FreshSchoolAdmissionAllowsExactMcpReplayWithoutReconsumingConfirmation()
    {
        var (f, command) = Fixture(ProfileCommandKind.CreateTeacher);
        f.Actor = f.Actor with { Channel = OperationChannel.MCP };
        command = command with { ConfirmationId = Guid.NewGuid() };
        var receipt = await f.Execute(command);
        f.Clock.Now = f.Clock.Now.AddMinutes(2);
        f.Sessions.ValidUntil = f.Clock.Now.AddMinutes(5);

        var replay = await f.Execute(command);

        Assert.Multiple(() => {
            Assert.That(replay, Is.EqualTo(receipt));
            Assert.That(f.Confirmations.Calls, Is.EqualTo(1));
            Assert.That(f.Schools.Calls, Is.EqualTo(2));
            Assert.That(f.Store.Operations, Has.Count.EqualTo(1));
        });
    }

    private static (ProfileServiceFixture Fixture, ProfileCommand Command) Fixture(ProfileCommandKind kind)
    {
        var f = new ProfileServiceFixture();
        var user = f.AddUser();
        if (kind == ProfileCommandKind.BootstrapAdministrator)
        {
            f.Access.PlatformOperatorIdentityIds = [f.Actor.IdentityId];
            f.Access.BootstrapWorkloads = ["school-bootstrap"];
            f.Actor = f.Actor with { Workload = "school-bootstrap" };
        }
        var command = f.Command(kind);
        if (kind == ProfileCommandKind.BootstrapAdministrator)
            command = command with { TargetUserId = user.UserId };
        return (f, command);
    }
}
