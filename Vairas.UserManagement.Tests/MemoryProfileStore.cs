using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Tests;

internal sealed class MemoryProfileStore : IProfileStore
{
    private sealed class State
    {
        public readonly Dictionary<Guid, UserProfile> Users = [];
        public readonly Dictionary<(ProfileKind Kind, Guid UserId), Guid> Roles = [];
        public readonly Dictionary<Guid, ProfileOperation> Operations = [];
        public readonly List<OutboxMessage> Outbox = [];
        public State Clone()
        {
            var copy = new State();
            foreach (var user in Users.Values) copy.Users.Add(user.UserId, new UserProfile {
                UserId = user.UserId, IdentityId = user.IdentityId, FirstName = user.FirstName, LastName = user.LastName,
                EmailAddress = user.EmailAddress, PhoneNumber = user.PhoneNumber, Status = user.Status,
                Revision = user.Revision, CreatedAt = user.CreatedAt, UpdatedAt = user.UpdatedAt });
            foreach (var pair in Roles) copy.Roles.Add(pair.Key, pair.Value);
            foreach (var pair in Operations) copy.Operations.Add(pair.Key, pair.Value);
            copy.Outbox.AddRange(Outbox);
            return copy;
        }
    }
    private State committed = new();
    private State? pending;
    private State Current => pending ?? committed;
    public IReadOnlyCollection<UserProfile> Users => committed.Users.Values;
    public IReadOnlyCollection<ProfileOperation> Operations => committed.Operations.Values;
    public IReadOnlyList<OutboxMessage> Outbox => committed.Outbox;
    public int Commits;
    public Exception? SaveError;
    public Action? OnBegin;
    public readonly List<string> Locks = [];
    public Task<IProfileTransaction> BeginAsync(CancellationToken ct)
    {
        if (pending is not null) throw new InvalidOperationException("Concurrent fake transactions are not supported.");
        OnBegin?.Invoke();
        pending = committed.Clone();
        return Task.FromResult<IProfileTransaction>(new Transaction(this));
    }
    public Task LockAsync(string key, CancellationToken ct) { Locks.Add(key); return Task.CompletedTask; }
    public Task<UserProfile?> FindUserAsync(Guid id, CancellationToken ct) => Task.FromResult(Current.Users.GetValueOrDefault(id));
    public Task<UserProfile?> FindIdentityAsync(Guid identityId, CancellationToken ct) => Task.FromResult(Current.Users.Values.SingleOrDefault(x => x.IdentityId == identityId));
    public Task<Guid?> FindRoleIdAsync(ProfileKind kind, Guid userId, CancellationToken ct) =>
        Task.FromResult(Current.Roles.TryGetValue((kind, userId), out var id) ? (Guid?)id : null);
    public Task<RoleProfileResult?> FindRoleAsync(ProfileKind kind, Guid profileId, CancellationToken ct)
    {
        var role = Current.Roles.SingleOrDefault(x => x.Key.Kind == kind && x.Value == profileId);
        return Task.FromResult(role.Equals(default(KeyValuePair<(ProfileKind, Guid), Guid>)) ? null :
            new RoleProfileResult(kind, profileId, role.Key.UserId, Current.Users[role.Key.UserId].Status == UserStatus.Active));
    }
    public Task<ProfileOperation?> FindOperationAsync(Guid id, CancellationToken ct) => Task.FromResult(Current.Operations.GetValueOrDefault(id));
    public void Add(UserProfile profile) => Current.Users.Add(profile.UserId, profile);
    public void Update(UserProfile profile) => Current.Users[profile.UserId] = profile;
    public void AddRole(ProfileKind kind, Guid id, Guid userId, DateTimeOffset now) => Current.Roles.Add((kind, userId), id);
    public void Add(ProfileOperation operation) => Current.Operations.Add(operation.OperationId, operation);
    public void Add(OutboxMessage message) => Current.Outbox.Add(message);
    public Task SaveAsync(CancellationToken ct) => SaveError is null ? Task.CompletedTask : Task.FromException(SaveError);
    private sealed class Transaction(MemoryProfileStore store) : IProfileTransaction
    {
        public Task CommitAsync(CancellationToken ct)
        {
            store.committed = store.pending ?? throw new InvalidOperationException("No transaction.");
            store.pending = null;
            store.Commits++;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() { store.pending = null; return ValueTask.CompletedTask; }
    }
}
