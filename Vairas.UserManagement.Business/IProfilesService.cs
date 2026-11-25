using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public interface IProfilesService
{
    Task<UserProfileResult> GetMyAsync(ActorContext actor, CancellationToken ct);
    Task<RoleProfileResult> GetMyRoleAsync(ProfileKind kind, ActorContext actor, CancellationToken ct);
    Task<OperationReceipt> ExecuteAsync(ProfileCommand command, ActorContext actor, CancellationToken ct);
    Task<UserProfile?> FindUserAsync(Guid userId, CancellationToken ct);
    Task<RoleProfileResult> FindRoleAsync(ProfileKind kind, Guid profileId, CancellationToken ct);
    Task<UserRoleProfilesResult> FindRoleProfilesAsync(Guid userId, CancellationToken ct);
}
