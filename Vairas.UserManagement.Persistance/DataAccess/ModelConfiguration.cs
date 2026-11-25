using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Persistance.DataAccess;

public static partial class ModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.HasPostgresEnum<UserStatus>(name: "um_user_status", nameTranslator: new Npgsql.NameTranslation.NpgsqlNullNameTranslator());
        model.HasPostgresEnum<OperationOutcome>(name: "um_operation_outcome", nameTranslator: new Npgsql.NameTranslation.NpgsqlNullNameTranslator());
        model.HasPostgresEnum<OperationChannel>(name: "um_channel", nameTranslator: new Npgsql.NameTranslation.NpgsqlNullNameTranslator());
        var user = model.Entity<UserProfile>();
        user.ToTable("user_profile", t =>
        {
            t.HasCheckConstraint("um_nonblank_name", "length(trim(first_name)) > 0 AND length(trim(last_name)) > 0");
            t.HasCheckConstraint("um_nonblank_email", "email_address IS NULL OR length(trim(email_address)) > 0");
            t.HasCheckConstraint("um_nonblank_phone", "phone_number IS NULL OR length(trim(phone_number)) > 0");
            t.HasCheckConstraint("um_profile_revision_time", "revision > 0 AND updated_at >= created_at");
        });
        user.HasKey(x => x.UserId).HasName("user_profile_pkey");
        user.HasIndex(x => x.IdentityId).IsUnique().HasDatabaseName("user_profile_identity_id_key");
        user.Property(x => x.FirstName).HasMaxLength(100);
        user.Property(x => x.LastName).HasMaxLength(100);
        user.Property(x => x.EmailAddress).HasMaxLength(254);
        user.Property(x => x.PhoneNumber).HasMaxLength(32);
        user.Property(x => x.Revision).IsConcurrencyToken().HasDefaultValue(1L);
        user.Property(x => x.Status).HasDefaultValue(UserStatus.Active);
        ConfigureRole<StudentProfile>(model, "student_profile", nameof(StudentProfile.StudentId));
        ConfigureRole<TeacherProfile>(model, "teacher_profile", nameof(TeacherProfile.TeacherId));
        ConfigureRole<AdministratorProfile>(model, "administrator_profile", nameof(AdministratorProfile.AdministratorId));
        var op = model.Entity<ProfileOperation>();
        op.ToTable("profile_operation", t =>
        {
            t.HasCheckConstraint("um_accountable_actor", "actor_user_id IS NOT NULL OR (actor_service IS NOT NULL AND length(trim(actor_service)) > 0)");
            t.HasCheckConstraint("um_expected_revision", "expected_revision IS NULL OR expected_revision > 0");
            t.HasCheckConstraint("um_result_revision", "resulting_revision IS NULL OR resulting_revision > 0");
            t.HasCheckConstraint("um_operation_basis", "length(request_fingerprint) = 64 AND length(trim(authority_basis)) > 0 AND length(trim(command_name)) > 0 AND length(trim(target_kind)) > 0 AND length(trim(outcome_code)) > 0");
            t.HasCheckConstraint("um_confirmation_pair", "(confirmation_id IS NULL) = (confirmed_at IS NULL)");
            t.HasCheckConstraint("um_mcp_write_confirmation", "channel <> 'MCP' OR outcome <> 'Applied' OR confirmation_id IS NOT NULL");
        });
        op.HasKey(x => x.OperationId).HasName("profile_operation_pkey");
        op.HasIndex(x => new { x.TargetKind, x.TargetId, x.RecordedAt });
        op.Property(x => x.ActorService).HasMaxLength(120);
        op.Property(x => x.AuthorityBasis).HasMaxLength(250);
        op.Property(x => x.CommandName).HasMaxLength(80);
        op.Property(x => x.TargetKind).HasMaxLength(80);
        op.Property(x => x.RequestFingerprint).HasMaxLength(64);
        op.Property(x => x.OutcomeCode).HasMaxLength(80);
        var outbox = model.Entity<OutboxMessage>();
        outbox.ToTable("outbox_message", t =>
        {
            t.HasCheckConstraint("outbox_version", "aggregate_version > 0 AND attempts >= 0");
            t.HasCheckConstraint("outbox_lease", "(lease_id IS NULL) = (leased_until IS NULL)");
        });
        outbox.HasKey(x => x.EventId);
        outbox.Property(x => x.Body).HasColumnType("jsonb");
        outbox.Property(x => x.EventType).HasMaxLength(80);
        outbox.Property(x => x.CorrelationId).HasMaxLength(128);
        outbox.Property(x => x.TraceParent).HasMaxLength(128);
        outbox.Property(x => x.LastErrorCode).HasMaxLength(80);
        outbox.HasIndex(x => new { x.NextAttemptAt, x.OccurredAt }).HasFilter("published_at IS NULL");
        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCase().Replace(property.Name, "$1_$2").ToLowerInvariant());
                if (property.ClrType == typeof(Guid)) property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
        }
    }

    private static void ConfigureRole<T>(ModelBuilder model, string table, string key) where T : class
    {
        var role = model.Entity<T>();
        role.ToTable(table);
        role.HasKey(key).HasName(table + "_pkey");
        role.HasIndex("UserId").IsUnique().HasDatabaseName(table + "_user_id_key");
        role.HasOne<UserProfile>().WithOne().HasForeignKey<T>("UserId")
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName(table + "_user_id_fkey");
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SnakeCase();
}
