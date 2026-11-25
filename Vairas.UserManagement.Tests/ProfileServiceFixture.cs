using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

// Ports only: these tests do not construct a DbContext, HTTP client, or database fixture.
internal sealed class ProfileServiceFixture
{
    public readonly TestClock Clock = new();
    public readonly MemoryProfileStore Store = new();
    public readonly SessionStub Sessions = new();
    public readonly SchoolStub Schools;
    public readonly ConfirmationStub Confirmations;
    public readonly ProfileAccessOptions Access = new();
    public ActorContext Actor = new(Guid.NewGuid(), Guid.NewGuid(), null, OperationChannel.Web, "unit-correlation", null);
    public ProfilesService Service { get; }

    public ProfileServiceFixture()
    {
        Schools = new(Clock);
        Confirmations = new(Clock);
        Sessions.ValidUntil = Clock.Now.AddMinutes(5);
        Service = new(Store, Sessions, Schools, Confirmations, Access, Clock, new ProfileCommandValidator());
    }

    public UserProfile AddUser(bool actor = true, UserStatus status = UserStatus.Active)
    {
        var user = new UserProfile { UserId = Guid.NewGuid(), IdentityId = actor ? Actor.IdentityId : Guid.NewGuid(),
            FirstName = "Ada", LastName = "Lovelace", EmailAddress = "ada@example.test", PhoneNumber = "+123456789",
            Status = status, CreatedAt = Clock.Now.AddDays(-1), UpdatedAt = Clock.Now.AddDays(-1) };
        Store.Add(user);
        return user;
    }

    public ProfileCommand Command(ProfileCommandKind kind = ProfileCommandKind.UpdateUser) => new()
    {
        OperationId = Guid.NewGuid(), Kind = kind,
        FirstName = kind is ProfileCommandKind.CreateUser or ProfileCommandKind.UpdateUser ? "Grace" : null,
        LastName = kind is ProfileCommandKind.CreateUser or ProfileCommandKind.UpdateUser ? "Hopper" : null,
        ExpectedRevision = kind is ProfileCommandKind.UpdateUser or ProfileCommandKind.Deactivate ? 1 : null,
        SchoolId = kind is ProfileCommandKind.CreateTeacher or ProfileCommandKind.CreateAdministrator or ProfileCommandKind.BootstrapAdministrator ? Guid.NewGuid() : null,
        InvitationId = kind is ProfileCommandKind.CreateTeacher or ProfileCommandKind.CreateAdministrator ? Guid.NewGuid() : null
    };
    public Task<OperationReceipt> Execute(ProfileCommand command) => Service.ExecuteAsync(command, Actor, default);
}
