SET LOCAL search_path TO telemedicina_m1, public;
DO $migration$
BEGIN
IF NOT EXISTS(SELECT 1 FROM schema_versions WHERE version=4) THEN
    ALTER TABLE challenges ADD COLUMN attempts integer NOT NULL DEFAULT 0 CHECK(attempts>=0);
    -- Old long verification tokens must be resent using the new six-character format.
    UPDATE challenges SET used=true WHERE purpose='verify' AND NOT used;
    DELETE FROM dev_mailbox WHERE purpose='verify';
    UPDATE email_outbox SET state='Superseded',payload_cipher=''
        WHERE kind='auth:verify' AND state='Queued';
    INSERT INTO schema_versions(version) VALUES(4);
END IF;
END $migration$;
