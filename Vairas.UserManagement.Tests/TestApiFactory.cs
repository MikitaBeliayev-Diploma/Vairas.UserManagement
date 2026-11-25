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

public sealed class TestApiFactory(string connection, DependencyServer server, Guid operatorIdentity) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<VairasDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<VairasDbContext>>();
            services.AddDbContext<VairasDbContext>(o => o.UseVairasPostgres(connection));
            services.Configure<DependencyOptions>(o =>
            {
                o.AuthBaseUrl = o.SchoolBaseUrl = o.ConfirmationBaseUrl = server.Url + "/";
                o.TokenEndpoint = server.Url + "/connect/token";
                o.ClientId = "user-management";
                o.ClientSecret = "test-only";
            });
            services.Configure<OutboxOptions>(o => o.Enabled = false);
            services.Configure<JwtOptions>(o => o.WorkloadClients = new()
            {
                ["school"] = ["profiles.status.read", "profiles.binding.read"],
                ["notifications"] = ["profiles.contact.read"],
                ["operators"] = ["profiles.deactivate", "profiles.bootstrap"],
                ["mcp"] = ["profiles.mcp.write"]
            });
            services.PostConfigure<JwtBearerOptions>("User", o => ConfigureJwt(o, "vairas-user-management"));
            services.PostConfigure<JwtBearerOptions>("Workload", o => ConfigureJwt(o, "vairas-user-management-internal"));
            services.AddSingleton(new ProfileAccessOptions { PlatformOperatorIdentityIds = [operatorIdentity], BootstrapWorkloads = ["operators"] });
        });
    }
    private void ConfigureJwt(JwtBearerOptions o, string audience)
    {
        o.Authority = server.Url;
        o.MetadataAddress = server.Url + "/.well-known/openid-configuration";
        o.RequireHttpsMetadata = false; // Loopback-only test issuer, never application configuration.
        o.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(o.MetadataAddress,
            new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = false });
        o.TokenValidationParameters.ValidIssuer = server.Url;
        o.TokenValidationParameters.ValidAudience = audience;
    }
}
