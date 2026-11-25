using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Integrations;

public sealed class DependencyTransport(IHttpClientFactory factory, IWorkloadTokenProvider tokens)
{
    public async Task<TResponse> PostAsync<TRequest, TResponse>(string baseUrl, string path, TRequest body, CancellationToken ct)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(baseUrl), path))
            { Content = JsonContent.Create(body, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { Converters = { new JsonStringEnumConverter() } }) };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(ct));
            using var response = await factory.CreateClient("dependencies").SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) throw ServiceErrors.Dependency();
            return await response.Content.ReadFromJsonAsync<TResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { Converters = { new JsonStringEnumConverter() } }, ct) ?? throw ServiceErrors.Dependency();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw ServiceErrors.Dependency(); }
    }
}
