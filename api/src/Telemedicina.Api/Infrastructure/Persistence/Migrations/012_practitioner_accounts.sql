SET LOCAL search_path TO telemedicina_m1, public;
DO $migration$
BEGIN
IF NOT EXISTS (SELECT 1 FROM schema_versions WHERE version=12) THEN
    ALTER TABLE practitioners ADD COLUMN IF NOT EXISTS user_id text REFERENCES users(id);
    CREATE UNIQUE INDEX IF NOT EXISTS practitioners_user_id ON practitioners(user_id)
        WHERE user_id IS NOT NULL;
    INSERT INTO schema_versions(version) VALUES(12);
END IF;
END $migration$;
