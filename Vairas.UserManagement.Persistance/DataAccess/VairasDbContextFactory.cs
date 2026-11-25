using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.NameTranslation;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Persistance.DataAccess;

public sealed class VairasDbContextFactory : IDesignTimeDbContextFactory<VairasDbContext>
{
    public VairasDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("VAIRAS_MIGRATION_CONNECTION")
            ?? "Host=localhost;Database=vairas_user_management;Username=v_user_management_migration";
        var options = new DbContextOptionsBuilder<VairasDbContext>();
        options.UseVairasPostgres(connection);
        return new(options.Options);
    }
}
