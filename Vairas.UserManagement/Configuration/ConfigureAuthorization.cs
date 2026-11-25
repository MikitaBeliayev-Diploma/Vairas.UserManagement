using Microsoft.AspNetCore.Authorization;
using Vairas.UserManagement.Security;

namespace Vairas.UserManagement.Configuration;

public static class ConfigureAuthorization
{
    private static readonly string[] WorkloadScopes =
    [
        "profiles.status.read", "profiles.contact.read", "profiles.documents.read",
        "profiles.binding.read", "profiles.deactivate", "profiles.bootstrap"
    ];

    public static IServiceCollection AddEntryAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ProfileEntry", policy => policy.AddRequirements(new EntryRequirement()));
            foreach (var scope in WorkloadScopes)
                options.AddPolicy(scope, policy => policy.AddRequirements(new EntryRequirement(scope)));
        });
        services.AddScoped<IAuthorizationHandler, EntryHandler>();
        services.AddScoped<IActorContextFactory, ActorContextFactory>();
        return services;
    }
}
