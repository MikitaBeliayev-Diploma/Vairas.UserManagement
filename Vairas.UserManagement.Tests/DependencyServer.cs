using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Integrations;
using Vairas.UserManagement.Persistance.DataAccess;
using Vairas.UserManagement.Security;
using Vairas.UserManagement.Workers;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Tests;

public sealed class DependencyServer : IAsyncDisposable
{
    private readonly RSA rsa = RSA.Create(2048);
    private WebApplication? app;
    public string Url { get; private set; } = "";
    public bool SessionActive { get; set; } = true;
    public bool SchoolAllowed { get; set; } = true;
    public bool ConfirmationAllowed { get; set; } = true;
    public bool Unavailable { get; set; }
    public bool ExpiredAuthority { get; set; }
    public bool WrongAuthorityTarget { get; set; }
    public bool ExpiredConfirmation { get; set; }
    public bool WrongConfirmationFingerprint { get; set; }
    public int TokenCalls { get; private set; }
    public System.Collections.Concurrent.ConcurrentQueue<(string Path, string CorrelationId)> DependencyCorrelations { get; } = new();

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        app = builder.Build();
        app.MapGet("/.well-known/openid-configuration", () => new { issuer = Url,
            jwks_uri = Url + "/jwks", authorization_endpoint = Url + "/authorize", token_endpoint = Url + "/connect/token" });
        app.MapGet("/jwks", () =>
        {
            var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = "test-key" });
            return new { keys = new[] { new { kty = key.Kty, kid = key.Kid, n = key.N, e = key.E, alg = "RS256", use = "sig" } } };
        });
        app.MapPost("/connect/token", async (Microsoft.AspNetCore.Http.HttpContext http) =>
        {
            TokenCalls++;
            var form = await http.Request.ReadFormAsync();
            return form["client_id"] == "user-management" && form["client_secret"] == "test-only"
                ? Microsoft.AspNetCore.Http.Results.Ok(new { access_token = "test-outbound", token_type = "Bearer", expires_in = 3600 })
                : Microsoft.AspNetCore.Http.Results.Unauthorized();
        });
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/internal") || context.Request.Path == "/connect/token")
                DependencyCorrelations.Enqueue((context.Request.Path.Value ?? "", context.Request.Headers["X-Correlation-ID"].ToString()));
            if (context.Request.Path.StartsWithSegments("/internal"))
            {
                if (Unavailable) { context.Response.StatusCode = 503; return; }
                if (context.Request.Headers.Authorization != "Bearer test-outbound") { context.Response.StatusCode = 401; return; }
            }
            await next(context);
        });
        app.MapPost("/internal/v1/sessions/validate", (SessionValidationRequest request) =>
            new SessionValidationResult(SessionActive, request.IdentityId, request.SessionId, DateTimeOffset.UtcNow.AddMinutes(1)));
        app.MapPost("/internal/v1/profile-provisioning/validate", (SchoolAuthorityRequest request) =>
            new SchoolAuthorityResult(SchoolAllowed, request.ActorUserId,
                WrongAuthorityTarget ? Guid.NewGuid() : request.TargetUserId, request.SchoolId, request.InvitationId,
                request.Role, request.Bootstrap, "school:test-invitation:v1",
                DateTimeOffset.UtcNow.AddMinutes(ExpiredAuthority ? -1 : 1), request.ActorUserId));
        app.MapPost("/internal/v1/confirmations/validate", (ConfirmationRequest request) =>
            new ConfirmationResult(ConfirmationAllowed, request.ConfirmationId, request.IdentityId, request.OperationId,
                WrongConfirmationFingerprint ? new string('0', 64) : request.RequestFingerprint, request.SchoolId, request.CommandName,
                DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(ExpiredConfirmation ? -1 : 1)));
        await app.StartAsync();
        Url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    public string Token(string subject, string audience = "vairas-user-management", string scopes = "", DateTime? expires = null)
    {
        var claims = new[] { new Claim("sub", subject), new Claim("sid", Guid.NewGuid().ToString()), new Claim("scope", scopes) };
        var until = expires ?? DateTime.UtcNow.AddMinutes(10);
        var token = new JwtSecurityToken(Url, audience, claims, DateTime.UtcNow.AddHours(-1), until,
            new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "test-key" }, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public async ValueTask DisposeAsync()
    {
        if (app is not null) await app.DisposeAsync();
        rsa.Dispose();
    }
}
