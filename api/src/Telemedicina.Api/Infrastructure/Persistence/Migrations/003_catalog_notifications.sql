SET LOCAL search_path TO telemedicina_m1, public;
DO $migration$
BEGIN
IF NOT EXISTS(SELECT 1 FROM schema_versions WHERE version=3) THEN
    ALTER TABLE services ADD COLUMN version integer NOT NULL DEFAULT 1;
    ALTER TABLE services ADD COLUMN is_synthetic boolean NOT NULL DEFAULT false;
    ALTER TABLE services ADD COLUMN approval_reference text;
    UPDATE services SET is_synthetic=true
        WHERE id='10000000-0000-4000-8000-000000000001';
    -- Existing rows are preserved; new or edited publications require explicit evidence.
    ALTER TABLE services ADD CONSTRAINT service_publication
        CHECK(NOT active OR is_synthetic OR coalesce(length(trim(approval_reference)),0)>0) NOT VALID;
    ALTER TABLE practitioners ADD COLUMN version integer NOT NULL DEFAULT 1;
    ALTER TABLE practitioners ADD COLUMN license_ref text;
    CREATE TABLE practitioner_services(
        practitioner_id text NOT NULL REFERENCES practitioners(id),
        service_id text NOT NULL REFERENCES services(id),
        active boolean NOT NULL DEFAULT true,
        PRIMARY KEY(practitioner_id,service_id));
    INSERT INTO practitioner_services(practitioner_id,service_id)
        SELECT DISTINCT practitioner_id,service_id FROM slots ON CONFLICT DO NOTHING;
    CREATE TABLE notification_preferences(
        user_id text PRIMARY KEY REFERENCES users(id),email_enabled boolean NOT NULL DEFAULT false,
        version integer NOT NULL DEFAULT 1);
    CREATE TABLE notifications(
        id text PRIMARY KEY,user_id text NOT NULL REFERENCES users(id),event_key text NOT NULL UNIQUE,
        kind text NOT NULL,body text NOT NULL,resource_id text,resource_version integer,
        created_at timestamptz NOT NULL DEFAULT now(),read_at timestamptz);
    CREATE INDEX notification_user ON notifications(user_id,created_at DESC);
    CREATE TABLE email_outbox(
        id text PRIMARY KEY,event_key text NOT NULL UNIQUE,
        notification_id text REFERENCES notifications(id),user_id text NOT NULL REFERENCES users(id),
        recipient text NOT NULL,kind text NOT NULL,resource_id text,resource_version integer,
        subject text NOT NULL,payload_cipher text NOT NULL,channel text NOT NULL,
        state text NOT NULL DEFAULT 'Queued' CHECK(state IN
            ('Queued','Sending','Sent','Captured','Superseded','Expired','Failed','Unknown')),
        attempts integer NOT NULL DEFAULT 0,available_at timestamptz NOT NULL DEFAULT now(),
        expires_at timestamptz,lease_token text,locked_until timestamptz,sent_at timestamptz,
        last_error_code text,created_at timestamptz NOT NULL DEFAULT now());
    CREATE INDEX outbox_pending ON email_outbox(channel,available_at) WHERE state='Queued';
    INSERT INTO schema_versions(version) VALUES(3);
END IF;
END $migration$;
