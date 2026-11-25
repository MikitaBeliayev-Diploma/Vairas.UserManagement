using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vairas.UserManagement.Persistance.DataAccess;

namespace Vairas.UserManagement.Persistance.Migrations;

[DbContext(typeof(VairasDbContext))]
[Migration("20261121000100_InitialProfiles")]
public sealed class InitialProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CanonicalSchema.sql")
            ?? throw new InvalidOperationException("Canonical schema resource is missing.");
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE profile_operation, administrator_profile, teacher_profile, student_profile, user_profile;
        DROP TYPE um_channel, um_operation_outcome, um_user_status;
        """);
}
