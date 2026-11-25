using NUnit.Framework;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class UnitTests
{
    private static readonly ActorContext Actor = new(Guid.NewGuid(), Guid.NewGuid(), null, OperationChannel.Web, "test", null);
    private static ProfileCommand Command => new() { OperationId = Guid.NewGuid(), Kind = ProfileCommandKind.UpdateUser,
        FirstName = "Test", LastName = "User", ExpectedRevision = 1 };

    [Test]
    public void FingerprintIgnoresTransportAndSessionButBindsArguments()
    {
        var command = Command;
        var hash = CommandFingerprint.Compute(command, Actor);
        Assert.That(hash, Has.Length.EqualTo(64));
        Assert.That(CommandFingerprint.Compute(command, Actor with { SessionId = Guid.NewGuid(), CorrelationId = "retry" }), Is.EqualTo(hash));
        Assert.That(CommandFingerprint.Compute(command with { ExpectedRevision = 2 }, Actor), Is.Not.EqualTo(hash));
        Assert.That(CommandFingerprint.Compute(command with { FirstName = " Test" }, Actor), Is.Not.EqualTo(hash));
        Assert.That(CommandFingerprint.Compute(command, Actor with { IdentityId = Guid.NewGuid() }), Is.Not.EqualTo(hash));
        Assert.That(CommandFingerprint.Compute(command, Actor with { Channel = OperationChannel.MCP }), Is.Not.EqualTo(hash));
        Assert.That(CommandFingerprint.Compute(command with { SchoolId = Guid.NewGuid() }, Actor), Is.Not.EqualTo(hash));
    }

    [Test]
    public void CanonicalFingerprintMatchesIndependentLanguageVector()
    {
        var actor = Actor with { IdentityId = Guid.Parse("10000000-0000-0000-0000-000000000001") };
        Assert.That(CommandFingerprint.Compute(Command, actor),
            Is.EqualTo("083f6b5a9aa8b979dd32c6b58647e1f30712c3175b55276cc9aa6c14f662ec0e"));
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t")]
    public void RejectsBlankNames(string name) => Assert.That(new ProfileCommandValidator().Validate(Command with { FirstName = name }).IsValid, Is.False);

    [Test]
    public void RequiresRevisionAndRoleInvitation()
    {
        var validator = new ProfileCommandValidator();
        Assert.That(validator.Validate(Command with { ExpectedRevision = null }).IsValid, Is.False);
        Assert.That(validator.Validate(Command with { Kind = ProfileCommandKind.CreateTeacher }).IsValid, Is.False);
        Assert.That(validator.Validate(Command with { Kind = ProfileCommandKind.CreateTeacher, SchoolId = Guid.NewGuid(), InvitationId = Guid.NewGuid() }).IsValid, Is.True);
    }
}
