using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public interface IConfirmationClient
{
    Task<ConfirmationResult> ValidateAsync(ConfirmationRequest request, CancellationToken ct);
}
