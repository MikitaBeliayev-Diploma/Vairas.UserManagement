using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vairas.UserManagement.Persistance.Migrations;

public partial class AddOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE outbox_message (
            event_id uuid PRIMARY KEY NOT NULL,
            aggregate_id uuid NOT NULL,
            aggregate_version bigint NOT NULL,
            event_type varchar(80) NOT NULL,
            body jsonb NOT NULL,
            correlation_id varchar(128) NOT NULL,
            trace_parent varchar(128),
            occurred_at timestamptz NOT NULL,
            next_attempt_at timestamptz NOT NULL,
            published_at timestamptz,
            lease_id uuid,
            leased_until timestamptz,
            attempts integer NOT NULL,
            last_error_code varchar(80),
            CONSTRAINT outbox_version CHECK (aggregate_version > 0 AND attempts >= 0),
            CONSTRAINT outbox_lease CHECK ((lease_id IS NULL) = (leased_until IS NULL))
        );
        CREATE INDEX "IX_outbox_message_next_attempt_at_occurred_at"
            ON outbox_message(next_attempt_at, occurred_at) WHERE published_at IS NULL;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("outbox_message");
}
