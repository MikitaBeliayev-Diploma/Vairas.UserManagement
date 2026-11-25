-- Fail before changing grants if administrator provisioning was omitted.
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_namespace n JOIN pg_roles r ON r.oid=n.nspowner
                WHERE n.nspname='public' AND r.rolname='v_user_management_migration') THEN
  RAISE EXCEPTION 'Run database/prepare-schema.sql as administrator before migrations and runtime grants';
 END IF;
END $$;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON SCHEMA public FROM v_user_management_runtime;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO v_user_management_runtime;
DO $$ BEGIN EXECUTE format('REVOKE CONNECT,CREATE,TEMPORARY ON DATABASE %I FROM PUBLIC',current_database()); END $$;
DO $$ BEGIN EXECUTE format('REVOKE CREATE,TEMPORARY ON DATABASE %I FROM v_user_management_runtime',current_database()); END $$;
DO $$ BEGIN EXECUTE format('GRANT CONNECT ON DATABASE %I TO v_user_management_runtime,v_user_management_migration',current_database()); END $$;
GRANT SELECT,INSERT ON "user_profile","student_profile","teacher_profile","administrator_profile","profile_operation" TO v_user_management_runtime;
GRANT UPDATE("first_name","last_name","email_address","phone_number","status","revision","updated_at") ON "user_profile" TO v_user_management_runtime;
GRANT SELECT,INSERT ON outbox_message TO v_user_management_runtime;
GRANT UPDATE(next_attempt_at,published_at,lease_id,leased_until,attempts,last_error_code)
  ON outbox_message TO v_user_management_runtime;
DO $secure$ DECLARE f record; BEGIN
 FOR f IN SELECT p.oid::regprocedure AS signature FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='public' AND p.prokind='f' AND NOT EXISTS(SELECT 1 FROM pg_depend dep WHERE dep.classid='pg_proc'::regclass AND dep.objid=p.oid AND dep.deptype='e') LOOP
  EXECUTE format('ALTER FUNCTION %s SET search_path=pg_catalog,public,pg_temp',f.signature);
 END LOOP;
END $secure$;
-- Direct revokes cannot neutralize rights inherited from other roles.
-- Reject an unsafe role configuration instead of silently reporting successful setup.
DO $$ BEGIN
 IF has_schema_privilege('v_user_management_runtime','public','CREATE')
    OR has_database_privilege('v_user_management_runtime',current_database(),'CREATE')
    OR has_database_privilege('v_user_management_runtime',current_database(),'TEMP')
    OR EXISTS (SELECT 1 FROM pg_roles WHERE rolname='v_user_management_runtime'
               AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
    OR EXISTS (SELECT 1 FROM pg_tables WHERE schemaname='public'
               AND pg_has_role('v_user_management_runtime',tableowner,'MEMBER')) THEN
  RAISE EXCEPTION 'User Management runtime role has unsafe effective privileges';
 END IF;
END $$;
