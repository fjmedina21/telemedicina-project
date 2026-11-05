-- Preserve revocation across access/refresh rotation, including concurrent logout.
SET LOCAL search_path TO telemedicina_m1, public;
DO $migration$
BEGIN
IF NOT EXISTS(SELECT 1 FROM schema_versions WHERE version=2) THEN
    ALTER TABLE sessions ADD COLUMN family_id text;
    UPDATE sessions SET family_id=id;
    ALTER TABLE sessions ALTER COLUMN family_id SET NOT NULL;
    CREATE INDEX session_family ON sessions(user_id,family_id);
    INSERT INTO schema_versions(version) VALUES(2);
END IF;
END $migration$;
