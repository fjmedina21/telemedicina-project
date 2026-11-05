-- H02-SPEC-v1; review and apply explicitly. Never applied during startup.
SET LOCAL search_path TO telemedicina_m1, public;
DO $migration$
BEGIN
IF NOT EXISTS(SELECT 1 FROM schema_versions WHERE version=5) THEN
    ALTER TABLE users DROP CONSTRAINT users_role_check;
    ALTER TABLE users ADD CONSTRAINT users_role_check
        CHECK(role IN ('Patient','Admission','Practitioner','Support','Administrator'));
    ALTER TABLE users ADD COLUMN version integer NOT NULL DEFAULT 1 CHECK(version>0);
    ALTER TABLE users ADD COLUMN created_at timestamptz NOT NULL DEFAULT now();
    ALTER TABLE users ADD COLUMN created_by text REFERENCES users(id);
    ALTER TABLE audit ADD COLUMN result text NOT NULL DEFAULT 'Legacy';
    ALTER TABLE audit ADD CONSTRAINT audit_result_check CHECK(result IN ('Legacy','Success','Denied'));
    CREATE TABLE staff_invitations(
        token_hash text PRIMARY KEY, user_id text NOT NULL REFERENCES users(id),
        expires bigint NOT NULL, used boolean NOT NULL DEFAULT false,
        issued_at bigint NOT NULL, created_by text NOT NULL REFERENCES users(id));
    CREATE INDEX staff_invitations_user ON staff_invitations(user_id);
    INSERT INTO schema_versions(version) VALUES(5);
END IF;
END $migration$;
