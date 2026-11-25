using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Integrations;

public sealed class SchoolAuthorityClient(DependencyTransport transport, IOptions<DependencyOptions> options) : ISchoolAuthorityClient
{
    public Task<SchoolAuthorityResult> ValidateAsync(SchoolAuthorityRequest request, CancellationToken ct) =>
        transport.PostAsync<SchoolAuthorityRequest, SchoolAuthorityResult>(options.Value.SchoolBaseUrl,
            "internal/v1/profile-provisioning/validate", request, ct);
}
