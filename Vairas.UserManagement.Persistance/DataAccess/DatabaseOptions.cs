using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.NameTranslation;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Persistance.DataAccess;

public static class DatabaseOptions
{
    private static readonly NpgsqlNullNameTranslator Names = new();

    public static DbContextOptionsBuilder UseVairasPostgres(this DbContextOptionsBuilder builder, string connection) =>
        builder.UseNpgsql(connection, options =>
        {
            options.MapEnum<UserStatus>("um_user_status", nameTranslator: Names);
            options.MapEnum<OperationOutcome>("um_operation_outcome", nameTranslator: Names);
            options.MapEnum<OperationChannel>("um_channel", nameTranslator: Names);
        });
}
