BEGIN;

DO $bootstrap$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_schema_owner') THEN
        CREATE ROLE dorosak_schema_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
        CREATE ROLE dorosak_runtime NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_migrator') THEN
        CREATE ROLE dorosak_migrator LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_app') THEN
        CREATE ROLE dorosak_app LOGIN INHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;

    IF EXISTS (
        SELECT
        FROM pg_roles
        WHERE rolname = 'dorosak_schema_owner'
          AND (rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)
    ) OR EXISTS (
        SELECT
        FROM pg_roles
        WHERE rolname = 'dorosak_runtime'
          AND (rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)
    ) OR EXISTS (
        SELECT
        FROM pg_roles
        WHERE rolname = 'dorosak_migrator'
          AND (NOT rolcanlogin OR rolinherit OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)
    ) OR EXISTS (
        SELECT
        FROM pg_roles
        WHERE rolname = 'dorosak_app'
          AND (NOT rolcanlogin OR NOT rolinherit OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)
    ) THEN
        RAISE EXCEPTION 'A Dorosak role exists with unsafe attributes.';
    END IF;
END
$bootstrap$;

GRANT dorosak_schema_owner TO dorosak_owner WITH ADMIN FALSE, INHERIT FALSE, SET TRUE;
GRANT dorosak_schema_owner TO dorosak_migrator WITH ADMIN FALSE, INHERIT FALSE, SET TRUE;
GRANT dorosak_runtime TO dorosak_app WITH ADMIN FALSE, INHERIT TRUE, SET FALSE;

DO $memberships$
BEGIN
    IF EXISTS (
        SELECT
        FROM pg_auth_members membership
        JOIN pg_roles granted_role ON granted_role.oid = membership.roleid
        JOIN pg_roles member_role ON member_role.oid = membership.member
        WHERE (
              member_role.rolname IN (
                  'dorosak_schema_owner',
                  'dorosak_runtime',
                  'dorosak_migrator',
                  'dorosak_app'
              )
              OR granted_role.rolname IN (
                  'dorosak_schema_owner',
                  'dorosak_runtime',
                  'dorosak_migrator',
                  'dorosak_app'
              )
          )
          AND NOT (
              member_role.rolname = 'dorosak_owner'
              AND granted_role.rolname IN (
                  'dorosak_schema_owner',
                  'dorosak_runtime',
                  'dorosak_migrator',
                  'dorosak_app'
              )
              AND NOT membership.inherit_option
              AND (
                  (membership.admin_option AND NOT membership.set_option)
                  OR (
                      granted_role.rolname = 'dorosak_schema_owner'
                      AND NOT membership.admin_option
                      AND membership.set_option
                  )
              )
          )
          AND NOT (
              member_role.rolname = 'dorosak_migrator'
              AND granted_role.rolname = 'dorosak_schema_owner'
              AND NOT membership.admin_option
              AND NOT membership.inherit_option
              AND membership.set_option
          )
          AND NOT (
              member_role.rolname = 'dorosak_app'
              AND granted_role.rolname = 'dorosak_runtime'
              AND NOT membership.admin_option
              AND membership.inherit_option
              AND NOT membership.set_option
          )
    ) OR NOT EXISTS (
        SELECT
        FROM pg_auth_members membership
        JOIN pg_roles granted_role ON granted_role.oid = membership.roleid
        JOIN pg_roles member_role ON member_role.oid = membership.member
        WHERE member_role.rolname = 'dorosak_migrator'
          AND granted_role.rolname = 'dorosak_schema_owner'
          AND NOT membership.admin_option
          AND NOT membership.inherit_option
          AND membership.set_option
    ) OR NOT EXISTS (
        SELECT
        FROM pg_auth_members membership
        JOIN pg_roles granted_role ON granted_role.oid = membership.roleid
        JOIN pg_roles member_role ON member_role.oid = membership.member
        WHERE member_role.rolname = 'dorosak_app'
          AND granted_role.rolname = 'dorosak_runtime'
          AND NOT membership.admin_option
          AND membership.inherit_option
          AND NOT membership.set_option
    ) THEN
        RAISE EXCEPTION 'A Dorosak role has an unexpected role membership.';
    END IF;
END
$memberships$;

REVOKE ALL ON SCHEMA public FROM PUBLIC, dorosak_runtime, dorosak_app;

CREATE SCHEMA IF NOT EXISTS app AUTHORIZATION dorosak_schema_owner;
CREATE SCHEMA IF NOT EXISTS operations AUTHORIZATION dorosak_schema_owner;
CREATE SCHEMA IF NOT EXISTS identity AUTHORIZATION dorosak_schema_owner;
CREATE SCHEMA IF NOT EXISTS profiles AUTHORIZATION dorosak_schema_owner;
CREATE SCHEMA IF NOT EXISTS migrations AUTHORIZATION dorosak_schema_owner;
ALTER SCHEMA app OWNER TO dorosak_schema_owner;
ALTER SCHEMA operations OWNER TO dorosak_schema_owner;
ALTER SCHEMA identity OWNER TO dorosak_schema_owner;
ALTER SCHEMA profiles OWNER TO dorosak_schema_owner;
ALTER SCHEMA migrations OWNER TO dorosak_schema_owner;

SET ROLE dorosak_schema_owner;

REVOKE ALL ON SCHEMA app FROM PUBLIC, dorosak_runtime, dorosak_app;
REVOKE ALL ON SCHEMA operations FROM PUBLIC, dorosak_runtime, dorosak_app;
REVOKE ALL ON SCHEMA identity FROM PUBLIC, dorosak_runtime, dorosak_app;
REVOKE ALL ON SCHEMA profiles FROM PUBLIC, dorosak_runtime, dorosak_app;
REVOKE ALL ON SCHEMA migrations FROM PUBLIC, dorosak_runtime, dorosak_app;
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA app, operations, identity, profiles FROM PUBLIC, dorosak_app;
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA app, operations, identity, profiles FROM dorosak_runtime;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA app, operations, identity, profiles FROM PUBLIC, dorosak_app;
REVOKE ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA app, operations, identity, profiles FROM PUBLIC, dorosak_app;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA app, operations, identity, profiles FROM dorosak_runtime;
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA app, operations, identity, profiles FROM dorosak_runtime;
GRANT USAGE ON SCHEMA app TO dorosak_runtime;
GRANT USAGE ON SCHEMA operations TO dorosak_runtime;
GRANT USAGE ON SCHEMA identity TO dorosak_runtime;
GRANT USAGE ON SCHEMA profiles TO dorosak_runtime;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA app TO dorosak_runtime;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA app TO dorosak_runtime;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA operations TO dorosak_runtime;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA operations TO dorosak_runtime;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO dorosak_runtime;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA identity TO dorosak_runtime;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA profiles TO dorosak_runtime;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA profiles TO dorosak_runtime;

DO $schema_marker$
BEGIN
    IF to_regclass('operations.schema_compatibility') IS NOT NULL THEN
        REVOKE INSERT, UPDATE, DELETE, TRUNCATE
            ON operations.schema_compatibility
            FROM dorosak_runtime;
    END IF;
END
$schema_marker$;

DO $append_only_security_events$
BEGIN
    IF to_regclass('identity.security_events') IS NOT NULL THEN
        REVOKE UPDATE, DELETE, TRUNCATE
            ON identity.security_events
            FROM dorosak_runtime;
        GRANT SELECT, INSERT
            ON identity.security_events
            TO dorosak_runtime;
    END IF;
END
$append_only_security_events$;

ALTER DEFAULT PRIVILEGES IN SCHEMA app REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA app REVOKE ALL ON SEQUENCES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA app REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA app REVOKE ALL ON SEQUENCES FROM dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA app REVOKE EXECUTE ON FUNCTIONS FROM dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA app GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA app GRANT USAGE ON SEQUENCES TO dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA operations REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA operations REVOKE ALL ON SEQUENCES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA operations REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA operations REVOKE ALL ON SEQUENCES FROM dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA operations REVOKE EXECUTE ON FUNCTIONS FROM dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA operations GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA operations GRANT USAGE ON SEQUENCES TO dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity REVOKE ALL ON SEQUENCES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity REVOKE ALL ON SEQUENCES FROM dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity REVOKE EXECUTE ON FUNCTIONS FROM dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity GRANT USAGE ON SEQUENCES TO dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA profiles REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA profiles REVOKE ALL ON SEQUENCES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA profiles REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA profiles REVOKE ALL ON SEQUENCES FROM dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA profiles REVOKE EXECUTE ON FUNCTIONS FROM dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA profiles GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dorosak_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA profiles GRANT USAGE ON SEQUENCES TO dorosak_runtime;

RESET ROLE;

DO $database_grants$
BEGIN
    EXECUTE format(
        'REVOKE CONNECT, CREATE, TEMPORARY ON DATABASE %I FROM PUBLIC, dorosak_runtime, dorosak_app',
        current_database()
    );
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO dorosak_migrator, dorosak_app', current_database());
    EXECUTE format('GRANT CREATE ON DATABASE %I TO dorosak_schema_owner', current_database());
    EXECUTE format(
        'ALTER ROLE dorosak_migrator IN DATABASE %I SET search_path = app, identity, profiles, operations, migrations, pg_catalog',
        current_database()
    );
    EXECUTE format(
        'ALTER ROLE dorosak_app IN DATABASE %I SET search_path = app, identity, profiles, operations, pg_catalog',
        current_database()
    );
    EXECUTE format(
        'ALTER ROLE dorosak_app IN DATABASE %I SET statement_timeout = %L',
        current_database(),
        '30s'
    );
    EXECUTE format(
        'ALTER ROLE dorosak_app IN DATABASE %I SET idle_in_transaction_session_timeout = %L',
        current_database(),
        '30s'
    );
END
$database_grants$;

COMMIT;
