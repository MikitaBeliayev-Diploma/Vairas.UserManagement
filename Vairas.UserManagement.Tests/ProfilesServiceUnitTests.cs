using System.Text.Json;
using FluentValidation;
using NUnit.Framework;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class ProfilesServiceUnitTests
{
    [Test]
    public async Task CreatingOwnProfilePersistsIdentityAndReplayReturnsTheSameReceipt()
    {
        var f = new ProfileServiceFixture();
        f.Clock.Now = f.Clock.Now.AddTicks(7);
        var command = f.Command(ProfileCommandKind.CreateUser) with { EmailAddress = "grace@example.test", PhoneNumber = "+123" };
        var first = await f.Execute(command);
        var replay = await f.Execute(command);
        var user = f.Store.Users.Single();
        Assert.Multiple(() => {
            Assert.That(first.Outcome, Is.EqualTo(OperationOutcome.Applied));
            Assert.That(replay, Is.EqualTo(first));
            Assert.That(user.IdentityId, Is.EqualTo(f.Actor.IdentityId));
            Assert.That(user.EmailAddress, Is.EqualTo(command.EmailAddress));
            Assert.That(user.PhoneNumber, Is.EqualTo(command.PhoneNumber));
            Assert.That(first.RecordedAt.Ticks % 10, Is.Zero);
            Assert.That(f.Store.Operations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task AnotherCreateIsRecordedAsRejectedWithoutReplacingTheExistingProfile()
    {
        var f = new ProfileServiceFixture(); var user = f.AddUser();
        var receipt = await f.Execute(f.Command(ProfileCommandKind.CreateUser));
        Assert.That(receipt.Code, Is.EqualTo("PROFILE_EXISTS"));
        Assert.That(receipt.Outcome, Is.EqualTo(OperationOutcome.Rejected));
        Assert.That(f.Store.Users.Single().UserId, Is.EqualTo(user.UserId));
    }

    [Test]
    public async Task UpdateChangesOnlyEditableFieldsAndAdvancesRevision()
    {
        var f = new ProfileServiceFixture(); var user = f.AddUser();
        var receipt = await f.Execute(f.Command());
        var updated = f.Store.Users.Single();
        Assert.Multiple(() => {
            Assert.That(updated.FirstName, Is.EqualTo("Grace"));
            Assert.That(updated.LastName, Is.EqualTo("Hopper"));
            Assert.That(updated.EmailAddress, Is.Null);
            Assert.That(updated.IdentityId, Is.EqualTo(user.IdentityId));
            Assert.That(updated.CreatedAt, Is.EqualTo(user.CreatedAt));
            Assert.That(updated.UpdatedAt, Is.EqualTo(f.Clock.Now));
            Assert.That(receipt.ResultingRevision, Is.EqualTo(2));
            Assert.That(f.Store.Outbox, Is.Empty);
        });
    }

    [Test]
    public async Task RevisionConflictIsDurableAndReplayDoesNotApplyTheUpdate()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); var command = f.Command() with { ExpectedRevision = 9 };
        var first = await f.Execute(command);
        Assert.That(await f.Execute(command), Is.EqualTo(first));
        Assert.That(first.Code, Is.EqualTo("REVISION_CONFLICT"));
        Assert.That(first.Outcome, Is.EqualTo(OperationOutcome.Rejected));
        Assert.That(f.Store.Users.Single().FirstName, Is.EqualTo("Ada"));
    }

    [Test]
    public async Task ReusingOperationForDifferentArgumentsIsRejectedWithoutMutation()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); var command = f.Command();
        await f.Execute(command);
        Error(() => f.Execute(command with { FirstName = "Different" }), 409, "IDEMPOTENCY_KEY_REUSED");
        Assert.That(f.Store.Users.Single().Revision, Is.EqualTo(2));
    }

    [TestCase(ProfileCommandKind.CreateStudent, ProfileKind.Student)]
    [TestCase(ProfileCommandKind.CreateTeacher, ProfileKind.Teacher)]
    [TestCase(ProfileCommandKind.CreateAdministrator, ProfileKind.Administrator)]
    public async Task RoleCreationHasAStableIdentityAndDoesNotDuplicateIt(ProfileCommandKind commandKind, ProfileKind role)
    {
        var f = new ProfileServiceFixture(); var user = f.AddUser();
        var first = await f.Execute(f.Command(commandKind));
        var second = await f.Execute(f.Command(commandKind));
        var mine = await f.Service.GetMyRoleAsync(role, f.Actor, default);
        Assert.Multiple(() => {
            Assert.That(first.TargetKind, Is.EqualTo(role + "Profile"));
            Assert.That(second.TargetId, Is.EqualTo(first.TargetId));
            Assert.That(second.Code, Is.EqualTo("ALREADY_EXISTS"));
            Assert.That(mine.ProfileId, Is.EqualTo(first.TargetId));
            Assert.That(mine.UserId, Is.EqualTo(user.UserId));
            Assert.That(mine.Active, Is.True);
            Assert.That(f.Store.Outbox, Is.Empty);
        });
        Assert.That(await f.Service.FindRoleAsync(role, first.TargetId, default), Is.EqualTo(mine));
    }

    [Test]
    public async Task ReadingOwnProfileReturnsContactsAndAllRoleIds()
    {
        var f = new ProfileServiceFixture(); var user = f.AddUser();
        var ids = Enum.GetValues<ProfileKind>().ToDictionary(k => k, _ => Guid.NewGuid());
        foreach (var pair in ids) f.Store.AddRole(pair.Key, pair.Value, user.UserId, f.Clock.Now);
        var result = await f.Service.GetMyAsync(f.Actor, default);
        Assert.Multiple(() => {
            Assert.That(result.UserId, Is.EqualTo(user.UserId));
            Assert.That(result.EmailAddress, Is.EqualTo(user.EmailAddress));
            Assert.That(result.StudentId, Is.EqualTo(ids[ProfileKind.Student]));
            Assert.That(result.TeacherId, Is.EqualTo(ids[ProfileKind.Teacher]));
            Assert.That(result.AdministratorId, Is.EqualTo(ids[ProfileKind.Administrator]));
        });
        Assert.That(await f.Service.FindUserAsync(user.UserId, default), Is.SameAs(user));
        Assert.That(await f.Service.FindUserAsync(Guid.NewGuid(), default), Is.Null);
    }

    [Test]
    public async Task RoleProfilesResolveAUserForDeactivationReconciliationEvenWhenInactive()
    {
        var f = new ProfileServiceFixture(); var user = f.AddUser();
        var teacher = Guid.NewGuid(); f.Store.AddRole(ProfileKind.Teacher, teacher, user.UserId, f.Clock.Now);
        user.Status = UserStatus.Inactive;
        var result = await f.Service.FindRoleProfilesAsync(user.UserId, default);
        Assert.Multiple(() => {
            Assert.That(result.UserId, Is.EqualTo(user.UserId));
            Assert.That(result.Status, Is.EqualTo(UserStatus.Inactive));
            Assert.That(result.Revision, Is.EqualTo(user.Revision));
            Assert.That(result.TeacherId, Is.EqualTo(teacher));
            Assert.That(result.StudentId, Is.Null);
            Assert.That(result.AdministratorId, Is.Null);
        });
        Error(() => f.Service.FindRoleProfilesAsync(Guid.NewGuid(), default), 404, "NOT_FOUND");
    }

    [Test]
    public void ReadsHideMissingProfilesAndRejectInactiveUsers()
    {
        var f = new ProfileServiceFixture();
        Error(() => f.Service.GetMyAsync(f.Actor, default), 404, "NOT_FOUND");
        f.AddUser();
        Error(() => f.Service.GetMyRoleAsync(ProfileKind.Student, f.Actor, default), 404, "NOT_FOUND");
        Error(() => f.Service.GetMyRoleAsync((ProfileKind)99, f.Actor, default), 404, "NOT_FOUND");
        Error(() => f.Service.FindRoleAsync(ProfileKind.Teacher, Guid.NewGuid(), default), 404, "NOT_FOUND");
        f.Store.Users.Single().Status = UserStatus.Inactive;
        Error(() => f.Service.GetMyAsync(f.Actor, default), 403, "ACCESS_DENIED");
    }

    [Test]
    public void MissingOrInactiveActorCannotWrite()
    {
        var f = new ProfileServiceFixture();
        Error(() => f.Execute(f.Command()), 403, "ACCESS_DENIED");
        f.AddUser(status: UserStatus.Inactive);
        Error(() => f.Execute(f.Command(ProfileCommandKind.CreateUser)), 403, "ACCESS_DENIED");
        Assert.That(f.Store.Operations, Is.Empty);
    }

    [Test]
    public void SessionMustRemainValidForBothReadsAndWrites()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); f.Sessions.ValidUntil = f.Clock.Now;
        Error(() => f.Service.GetMyAsync(f.Actor, default), 503, "DEPENDENCY_UNAVAILABLE");
        Error(() => f.Execute(f.Command()), 503, "DEPENDENCY_UNAVAILABLE");
        Assert.That(f.Store.Operations, Is.Empty);
    }

    [Test]
    public void ActivityIsRecheckedAfterEnteringTransaction()
    {
        var f = new ProfileServiceFixture(); var user = f.AddUser();
        f.Store.OnBegin = () => user.Status = UserStatus.Inactive;
        Error(() => f.Execute(f.Command()), 403, "ACCESS_DENIED");
        Assert.That(f.Store.Operations, Is.Empty);
    }

    [Test]
    public void InvalidCommandsDoNotReachAuthenticationOrPersistence()
    {
        var f = new ProfileServiceFixture(); f.Sessions.Error = new InvalidOperationException("Must not authenticate invalid input.");
        Assert.ThrowsAsync<ValidationException>(() => f.Execute(f.Command() with { FirstName = " " }));
        Assert.That(f.Store.Commits, Is.Zero);
    }

    [Test]
    public async Task OperatorDeactivationWritesAnAtomicEventAndSecondCommandIsANoOp()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); var target = f.AddUser(false);
        f.Access.PlatformOperatorIdentityIds = [f.Actor.IdentityId];
        var command = f.Command(ProfileCommandKind.Deactivate) with { TargetUserId = target.UserId, Reason = "account closed" };
        var first = await f.Execute(command);
        var second = await f.Execute(command with { OperationId = Guid.NewGuid(), ExpectedRevision = 2 });
        var message = f.Store.Outbox.Single();
        using var body = JsonDocument.Parse(message.Body);
        Assert.Multiple(() => {
            Assert.That(first.ResultingRevision, Is.EqualTo(2));
            Assert.That(second.Code, Is.EqualTo("ALREADY_INACTIVE"));
            Assert.That(message.AggregateId, Is.EqualTo(target.UserId));
            Assert.That(message.AggregateVersion, Is.EqualTo(2));
            Assert.That(body.RootElement.GetProperty("payload").GetProperty("userId").GetGuid(), Is.EqualTo(target.UserId));
            Assert.That(body.RootElement.GetProperty("headers").GetProperty("causationId").GetString(), Is.EqualTo(command.OperationId.ToString()));
            Assert.That(f.Store.Operations.First().Reason, Is.EqualTo("account closed"));
        });
    }

    [Test]
    public void FailedSaveRollsBackProfileOperationAndOutboxTogether()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); var target = f.AddUser(false);
        f.Access.PlatformOperatorIdentityIds = [f.Actor.IdentityId];
        f.Store.SaveError = new InvalidOperationException("Storage unavailable");
        Assert.ThrowsAsync<InvalidOperationException>(() => f.Execute(f.Command(ProfileCommandKind.Deactivate) with { TargetUserId = target.UserId }));
        Assert.Multiple(() => {
            Assert.That(f.Store.Users.Single(x => x.UserId == target.UserId).Status, Is.EqualTo(UserStatus.Active));
            Assert.That(f.Store.Users.Single(x => x.UserId == target.UserId).Revision, Is.EqualTo(1));
            Assert.That(f.Store.Operations, Is.Empty);
            Assert.That(f.Store.Outbox, Is.Empty);
            Assert.That(f.Store.Commits, Is.Zero);
        });
    }

    [Test]
    public async Task UnknownOperatorTargetProducesADurableNotFoundReceipt()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); f.Access.PlatformOperatorIdentityIds = [f.Actor.IdentityId];
        var receipt = await f.Execute(f.Command(ProfileCommandKind.Deactivate) with { TargetUserId = Guid.NewGuid() });
        Assert.That(receipt.Code, Is.EqualTo("NOT_FOUND"));
        Assert.That(receipt.Outcome, Is.EqualTo(OperationOutcome.Rejected));
    }

    [Test]
    public async Task BootstrapRequiresAnOperatorWorkloadAndCreatesRoleForTheRequestedUser()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); var target = f.AddUser(false);
        var command = f.Command(ProfileCommandKind.BootstrapAdministrator) with { TargetUserId = target.UserId };
        Error(() => f.Execute(command), 403, "ACCESS_DENIED");
        f.Access.PlatformOperatorIdentityIds = [f.Actor.IdentityId];
        Error(() => f.Execute(command), 403, "ACCESS_DENIED");
        f.Actor = f.Actor with { Workload = "bootstrap" }; f.Access.BootstrapWorkloads = ["bootstrap"];
        var receipt = await f.Execute(command);
        var role = await f.Service.FindRoleAsync(ProfileKind.Administrator, receipt.TargetId, default);
        Assert.That(role.UserId, Is.EqualTo(target.UserId));
        Assert.That(f.Store.Operations.Single().AuthorityBasis, Is.EqualTo("invitation:unit"));
    }

    [Test]
    public async Task BootstrapCannotCreateARoleForInactiveTarget()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); var target = f.AddUser(false, UserStatus.Inactive);
        f.Access.PlatformOperatorIdentityIds = [f.Actor.IdentityId]; f.Access.BootstrapWorkloads = ["bootstrap"];
        f.Actor = f.Actor with { Workload = "bootstrap" };
        var receipt = await f.Execute(f.Command(ProfileCommandKind.BootstrapAdministrator) with { TargetUserId = target.UserId });
        Assert.That(receipt.Code, Is.EqualTo("USER_INACTIVE"));
        Assert.That(receipt.Outcome, Is.EqualTo(OperationOutcome.Rejected));
    }

    [TestCase("denied")]
    [TestCase("target")]
    [TestCase("school")]
    [TestCase("role")]
    [TestCase("basis")]
    [TestCase("invitation")]
    public void SchoolAuthorityMustMatchTheExactAdmission(string mismatch)
    {
        var f = new ProfileServiceFixture(); f.AddUser();
        f.Schools.Reply = r => mismatch switch {
            "denied" => r with { Allowed = false }, "target" => r with { TargetUserId = Guid.NewGuid() },
            "school" => r with { SchoolId = Guid.NewGuid() }, "role" => r with { Role = ProfileKind.Student },
            "basis" => r with { AuthorityBasis = " " }, _ => r with { InvitationId = Guid.NewGuid() }
        };
        Error(() => f.Execute(f.Command(ProfileCommandKind.CreateTeacher)), 403, "ACCESS_DENIED");
        Assert.That(f.Store.Operations, Is.Empty);
    }

    [Test]
    public void ExpiredSchoolDecisionCannotCreateAProfile()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); f.Schools.Reply = r => r with { ValidUntil = f.Clock.Now };
        Error(() => f.Execute(f.Command(ProfileCommandKind.CreateTeacher)), 503, "DEPENDENCY_UNAVAILABLE");
        Assert.That(f.Store.Operations, Is.Empty);
    }

    [Test]
    public async Task McpRequiresExactConfirmationAndReplayDoesNotConsumeItAgain()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); f.Actor = f.Actor with { Channel = OperationChannel.MCP };
        var command = f.Command();
        Error(() => f.Execute(command), 422, "CONFIRMATION_REQUIRED");
        command = command with { ConfirmationId = Guid.NewGuid() };
        var first = await f.Execute(command);
        Assert.That(await f.Execute(command), Is.EqualTo(first));
        Assert.That(f.Confirmations.Calls, Is.EqualTo(1));
        Assert.That(f.Store.Operations.Single().ConfirmationId, Is.EqualTo(command.ConfirmationId));
    }

    [TestCase("denied")]
    [TestCase("fingerprint")]
    [TestCase("future")]
    [TestCase("interval")]
    public void McpRejectsUnboundOrInvalidConfirmation(string mismatch)
    {
        var f = new ProfileServiceFixture(); f.AddUser(); f.Actor = f.Actor with { Channel = OperationChannel.MCP };
        f.Confirmations.Reply = r => mismatch switch {
            "denied" => r with { Allowed = false }, "fingerprint" => r with { RequestFingerprint = "other" },
            "future" => r with { ConfirmedAt = f.Clock.Now.AddSeconds(5) }, _ => r with { ExpiresAt = r.ConfirmedAt }
        };
        Error(() => f.Execute(f.Command() with { ConfirmationId = Guid.NewGuid() }), 422, "CONFIRMATION_INVALID");
        Assert.That(f.Store.Operations, Is.Empty);
    }

    [Test]
    public void ConfirmationExpirationBetweenValidationAndTransactionIsRejected()
    {
        var f = new ProfileServiceFixture(); f.AddUser(); f.Actor = f.Actor with { Channel = OperationChannel.MCP };
        f.Store.OnBegin = () => f.Clock.Now = f.Clock.Now.AddMinutes(2);
        Error(() => f.Execute(f.Command() with { ConfirmationId = Guid.NewGuid() }), 422, "CONFIRMATION_EXPIRED");
        Assert.That(f.Store.Operations, Is.Empty);
    }

    private static void Error(Func<Task> action, int status, string code)
    {
        var error = Business.ThrowsBusinessAsync(async () => await action());
        Assert.That(error.HttpStatus(), Is.EqualTo(status));
        Assert.That(error.Code, Is.EqualTo(code));
    }
}
