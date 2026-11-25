-- Run as database administrator against the empty User Management database, before EF migrations.
-- Database ownership alone does not transfer public schema ownership on PostgreSQL 14.
ALTER SCHEMA public OWNER TO v_user_management_migration;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE,CREATE ON SCHEMA public TO v_user_management_migration;
