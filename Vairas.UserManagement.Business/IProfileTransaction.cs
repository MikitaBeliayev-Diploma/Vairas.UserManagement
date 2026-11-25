using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public interface IProfileTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
