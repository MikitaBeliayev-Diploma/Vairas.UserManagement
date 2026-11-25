using Vairas.UserManagement.Business;
using Vairas.UserManagement.Integrations;

namespace Vairas.UserManagement.Configuration;

public static class ConfigureHttpClients
{
    public static IServiceCollection AddDependencyHttpClients(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<DependencyCorrelationHandler>();
        services.AddHttpClient("dependencies", http => http.Timeout = TimeSpan.FromSeconds(5))
            .AddHttpMessageHandler<DependencyCorrelationHandler>();
        services.AddSingleton<IWorkloadTokenProvider, WorkloadTokenProvider>();
        services.AddScoped<DependencyTransport>();
        services.AddScoped<IAuthSessionValidator, AuthSessionClient>();
        services.AddScoped<ISchoolAuthorityClient, SchoolAuthorityClient>();
        services.AddScoped<IConfirmationClient, ConfirmationClient>();
        return services;
    }
}
