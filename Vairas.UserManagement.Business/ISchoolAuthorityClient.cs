using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public interface ISchoolAuthorityClient
{
    Task<SchoolAuthorityResult> ValidateAsync(SchoolAuthorityRequest request, CancellationToken ct);
}
