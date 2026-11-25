CREATE TYPE "um_user_status" AS ENUM (
  'Active',
  'Inactive'
);

CREATE TYPE "um_operation_outcome" AS ENUM (
  'Applied',
  'Rejected'
);

CREATE TYPE "um_channel" AS ENUM (
  'Web',
  'MCP',
  'Internal'
);

CREATE TABLE "user_profile" (
  "user_id" uuid PRIMARY KEY NOT NULL,
  "identity_id" uuid UNIQUE NOT NULL,
  "first_name" varchar(100) NOT NULL,
  "last_name" varchar(100) NOT NULL,
  "email_address" varchar(254),
  "phone_number" varchar(32),
  "status" um_user_status NOT NULL DEFAULT 'Active',
  "revision" bigint NOT NULL DEFAULT 1,
  "created_at" timestamptz NOT NULL,
  "updated_at" timestamptz NOT NULL,
  CONSTRAINT "um_nonblank_name" CHECK (length(trim(first_name)) > 0 AND length(trim(last_name)) > 0),
  CONSTRAINT "um_nonblank_email" CHECK (email_address IS NULL OR length(trim(email_address)) > 0),
  CONSTRAINT "um_nonblank_phone" CHECK (phone_number IS NULL OR length(trim(phone_number)) > 0),
  CONSTRAINT "um_profile_revision_time" CHECK (revision > 0 AND updated_at >= created_at)
);

CREATE TABLE "student_profile" (
  "student_id" uuid PRIMARY KEY NOT NULL,
  "user_id" uuid UNIQUE NOT NULL,
  "created_at" timestamptz NOT NULL
);

CREATE TABLE "teacher_profile" (
  "teacher_id" uuid PRIMARY KEY NOT NULL,
  "user_id" uuid UNIQUE NOT NULL,
  "created_at" timestamptz NOT NULL
);

CREATE TABLE "administrator_profile" (
  "administrator_id" uuid PRIMARY KEY NOT NULL,
  "user_id" uuid UNIQUE NOT NULL,
  "created_at" timestamptz NOT NULL
);

CREATE TABLE "profile_operation" (
  "operation_id" uuid PRIMARY KEY NOT NULL,
  "actor_user_id" uuid,
  "actor_service" varchar(120),
  "requesting_school_id" uuid,
  "authority_basis" varchar(250) NOT NULL,
  "channel" um_channel NOT NULL,
  "command_name" varchar(80) NOT NULL,
  "target_kind" varchar(80) NOT NULL,
  "target_id" uuid NOT NULL,
  "expected_revision" bigint,
  "resulting_revision" bigint,
  "request_fingerprint" varchar(64) NOT NULL,
  "outcome" um_operation_outcome NOT NULL,
  "outcome_code" varchar(80) NOT NULL,
  "recorded_at" timestamptz NOT NULL,
  "reason" text,
  "confirmation_id" uuid,
  "confirmed_at" timestamptz,
  CONSTRAINT "um_accountable_actor" CHECK (actor_user_id IS NOT NULL OR (actor_service IS NOT NULL AND length(trim(actor_service)) > 0)),
  CONSTRAINT "um_expected_revision" CHECK (expected_revision IS NULL OR expected_revision > 0),
  CONSTRAINT "um_result_revision" CHECK (resulting_revision IS NULL OR resulting_revision > 0),
  CONSTRAINT "um_operation_basis" CHECK (length(request_fingerprint) = 64 AND length(trim(authority_basis)) > 0 AND length(trim(command_name)) > 0 AND length(trim(target_kind)) > 0 AND length(trim(outcome_code)) > 0),
  CONSTRAINT "um_confirmation_pair" CHECK ((confirmation_id IS NULL) = (confirmed_at IS NULL)),
  CONSTRAINT "um_mcp_write_confirmation" CHECK (channel <> 'MCP' OR outcome <> 'Applied' OR confirmation_id IS NOT NULL)
);

CREATE INDEX ON "profile_operation" ("target_kind", "target_id", "recorded_at");

























ALTER TABLE "student_profile" ADD FOREIGN KEY ("user_id") REFERENCES "user_profile" ("user_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "teacher_profile" ADD FOREIGN KEY ("user_id") REFERENCES "user_profile" ("user_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "administrator_profile" ADD FOREIGN KEY ("user_id") REFERENCES "user_profile" ("user_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;
