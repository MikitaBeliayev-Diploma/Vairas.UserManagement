using NUnit.Framework;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class InvitationIssuerActivityTests
{
    [TestCase(ProfileCommandKind.CreateTeacher)]
    [TestCase(ProfileCommandKind.CreateAdministrator)]
    [TestCase(ProfileCommandKind.BootstrapAdministrator)]
    public async Task InactiveIssuerCannotCreateFirstStaffProfile(ProfileCommandKind kind)
    {
        var (f, command) = Fixture(kind);
        var issuer = f.AddUser(actor: false, status: UserStatus.Inactive);
        f.Schools.Reply = r => r with { IssuerUserId = issuer.UserId };

        Assert.That(Business.ThrowsBusinessAsync(() => f.Execute(command)).HttpStatus(), Is.EqualTo(403));
        Assert.That(f.Store.Operations, Is.Empty);
        Assert.That(await f.Store.FindRoleIdAsync(Role(kind), f.Store.Users.First().UserId, default), Is.Null);
        Assert.That(f.Store.Commits, Is.Zero);
    }

    [TestCase("missing")]
    [TestCase("empty")]
    [TestCase("unknown")]
    public void AnAllowedResponseWithoutAKnownIssuerFailsClosed(string issuer)
    {
        var (f, command) = Fixture(ProfileCommandKind.CreateTeacher);
        f.Schools.Reply = r => r with { IssuerUserId = issuer switch { "missing" => null, "empty" => Guid.Empty, _ => Guid.NewGuid() } };
        Assert.That(Business.ThrowsBusinessAsync(() => f.Execute(command)).HttpStatus(), Is.EqualTo(403));
        Assert.That(f.Store.Operations, Is.Empty);
    }

    [Test]
    public async Task PrimaryIssuerActivityIsReadAfterSortedUserLocks()
    {
        var (f, command) = Fixture(ProfileCommandKind.CreateTeacher);
        var issuer = f.AddUser(actor: false);
        f.Schools.Reply = r => r with { IssuerUserId = issuer.UserId };
        // Simulates a deactivation committed after the School response and before admission.
        f.Store.OnBegin = () => issuer.Status = UserStatus.Inactive;
        Assert.That(Business.ThrowsBusinessAsync(() => f.Execute(command)).HttpStatus(), Is.EqualTo(403));
        var userLocks = f.Store.Locks.Skip(1).ToArray();
        Assert.That(userLocks, Is.EqualTo(userLocks.Order(StringComparer.Ordinal).ToArray()));
        Assert.That(userLocks, Does.Contain($"user:{issuer.UserId:D}"));
        Assert.That(f.Store.Commits, Is.Zero);
        Assert.That(await f.Store.FindRoleIdAsync(ProfileKind.Teacher, f.Store.Users.First().UserId, default), Is.Null);
    }

    [Test]
    public async Task ActiveIssuerAllowsFirstCreationAndItsLaterDeactivationDoesNotRewriteReceipt()
    {
        var (f, command) = Fixture(ProfileCommandKind.CreateTeacher);
        var issuer = f.AddUser(actor: false);
        f.Schools.Reply = r => r with { IssuerUserId = issuer.UserId };
        var receipt = await f.Execute(command);
        (await f.Store.FindUserAsync(issuer.UserId, default))!.Status = UserStatus.Inactive;
        Assert.That(await f.Execute(command), Is.EqualTo(receipt));
        Assert.That(f.Store.Operations, Has.Count.EqualTo(1));
        var again = await f.Execute(command with { OperationId = Guid.NewGuid() });
        Assert.That(again.Code, Is.EqualTo("ALREADY_EXISTS"));
    }

    private static ProfileKind Role(ProfileCommandKind kind) => kind == ProfileCommandKind.CreateTeacher ? ProfileKind.Teacher : ProfileKind.Administrator;
    private static (ProfileServiceFixture, ProfileCommand) Fixture(ProfileCommandKind kind)
    {
        var f = new ProfileServiceFixture();
        var target = f.AddUser();
        if (kind == ProfileCommandKind.BootstrapAdministrator)
        {
            f.Access.PlatformOperatorIdentityIds = [f.Actor.IdentityId];
            f.Access.BootstrapWorkloads = ["operators"];
            f.Actor = f.Actor with { Workload = "operators" };
        }
        return (f, f.Command(kind) with { TargetUserId = kind == ProfileCommandKind.BootstrapAdministrator ? target.UserId : null });
    }
}
