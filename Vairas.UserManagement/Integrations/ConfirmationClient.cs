using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Integrations;

public sealed class ConfirmationClient(DependencyTransport transport, IOptions<DependencyOptions> options) : IConfirmationClient
{
    public Task<ConfirmationResult> ValidateAsync(ConfirmationRequest request, CancellationToken ct) =>
        transport.PostAsync<ConfirmationRequest, ConfirmationResult>(options.Value.ConfirmationBaseUrl,
            "internal/v1/confirmations/validate", request, ct);
}
