using FluentValidation;
using Vairas.UserManagement.Business;

namespace Vairas.UserManagement.Configuration;

/// <summary>Application services; infrastructure registrations live in the dedicated Configure* classes.</summary>
public static class ConfigureDependencyInjection
{
    public static IServiceCollection AddProfileServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IProfilesService, ProfilesService>();
        services.AddScoped<IValidator<ProfileCommand>, ProfileCommandValidator>();
        return services;
    }
}
