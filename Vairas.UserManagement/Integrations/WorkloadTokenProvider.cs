using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Integrations;

public sealed class WorkloadTokenProvider(IHttpClientFactory factory, IOptions<DependencyOptions> options,
    TimeProvider clock) : IWorkloadTokenProvider, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? token;
    private DateTimeOffset expiresAt;

    public async Task<string> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (token is not null && expiresAt > clock.GetUtcNow().AddSeconds(30)) return token;
            var settings = options.Value;
            if (string.IsNullOrWhiteSpace(settings.ClientSecret)) throw ServiceErrors.Dependency();
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials", ["client_id"] = settings.ClientId,
                    ["client_secret"] = settings.ClientSecret, ["scope"] = settings.Scope
                })
            };
            using var response = await factory.CreateClient("dependencies").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw ServiceErrors.Dependency();
            var result = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            if (result is null || string.IsNullOrWhiteSpace(result.AccessToken) || result.ExpiresIn <= 30 ||
                !string.Equals(result.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase)) throw ServiceErrors.Dependency();
            token = result.AccessToken;
            expiresAt = clock.GetUtcNow().AddSeconds(Math.Min(result.ExpiresIn, 86400));
            return token;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw ServiceErrors.Dependency();
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
