using Microsoft.Extensions.Options;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Configuration.Options;

namespace Vairas.UserManagement.Configuration;

/// <summary>Binds and validates every strongly typed options section; invalid required settings fail at startup.</summary>
public static class ConfigureOptions
{
    public static IServiceCollection AddUserManagementOptions(this IServiceCollection services, IConfiguration configuration)
    {
        AddValidated<JwtOptions, JwtOptionsValidator>(services, configuration.GetSection("Authentication"));
        AddValidated<DependencyOptions, DependencyOptionsValidator>(services, configuration.GetSection("Dependencies"));
        AddValidated<OutboxOptions, OutboxOptionsValidator>(services, configuration.GetSection("Outbox"));
        services.AddOptions<ProfileAccessOptions>().Bind(configuration.GetSection("ProfileAccess"));
        // The business layer takes the plain settings object so it does not depend on the options infrastructure.
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<ProfileAccessOptions>>().Value);
        return services;
    }

    private static void AddValidated<TOptions, TValidator>(IServiceCollection services, IConfigurationSection section)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.AddOptions<TOptions>().Bind(section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
    }
}
