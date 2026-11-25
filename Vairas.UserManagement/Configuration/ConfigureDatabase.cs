using Vairas.UserManagement.Business;
using Vairas.UserManagement.Persistance;
using Vairas.UserManagement.Persistance.DataAccess;

namespace Vairas.UserManagement.Configuration;

public static class ConfigureDatabase
{
    private const string LocalRuntimeConnection = "Host=localhost;Database=vairas_user_management;Username=v_user_management_runtime";

    public static IServiceCollection AddUserManagementDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("UserManagement") ?? LocalRuntimeConnection;
        services.AddDbContext<VairasDbContext>(options => options.UseVairasPostgres(connection));
        services.AddScoped<IProfileStore, ProfileStore>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        return services;
    }
}
