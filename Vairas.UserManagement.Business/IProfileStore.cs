using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public interface IProfileStore
{
    Task<IProfileTransaction> BeginAsync(CancellationToken ct);
    Task LockAsync(string key, CancellationToken ct);
    Task<UserProfile?> FindUserAsync(Guid id, CancellationToken ct);
    Task<UserProfile?> FindIdentityAsync(Guid identityId, CancellationToken ct);
    Task<Guid?> FindRoleIdAsync(ProfileKind kind, Guid userId, CancellationToken ct);
    Task<RoleProfileResult?> FindRoleAsync(ProfileKind kind, Guid profileId, CancellationToken ct);
    Task<ProfileOperation?> FindOperationAsync(Guid id, CancellationToken ct);
    void Add(UserProfile profile);
    void Update(UserProfile profile);
    void AddRole(ProfileKind kind, Guid id, Guid userId, DateTimeOffset now);
    void Add(ProfileOperation operation);
    void Add(OutboxMessage message);
    Task SaveAsync(CancellationToken ct);
}
