-- Review and apply explicitly using --migrate. Never run automatically on API startup.
-- Dedicated schema; no changes to existing public tables. Synthetic data only.
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE SCHEMA IF NOT EXISTS telemedicina_m1;
SET LOCAL search_path TO telemedicina_m1, public;
CREATE TABLE IF NOT EXISTS schema_versions(version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());
DO $migration$
BEGIN
IF NOT EXISTS (SELECT 1 FROM schema_versions WHERE version=1) THEN
CREATE TABLE users (
    id text PRIMARY KEY, email text NOT NULL, password_hash text NOT NULL,
    role text NOT NULL CHECK(role IN ('Patient','Admission')), verified boolean NOT NULL DEFAULT false,
    active boolean NOT NULL DEFAULT true, totp_secret text, last_totp_step bigint NOT NULL DEFAULT -1,
    failed_logins integer NOT NULL DEFAULT 0, locked_until bigint NOT NULL DEFAULT 0);
CREATE UNIQUE INDEX users_email ON users(lower(email));
CREATE TABLE profiles(user_id text PRIMARY KEY REFERENCES users(id), body text NOT NULL, version integer NOT NULL DEFAULT 1, updated_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE sessions(
    id text PRIMARY KEY, user_id text NOT NULL REFERENCES users(id),
    access_hash text NOT NULL UNIQUE, refresh_hash text NOT NULL UNIQUE,
    access_expires bigint NOT NULL, expires bigint NOT NULL, revoked boolean NOT NULL DEFAULT false);
CREATE TABLE challenges(
    token_hash text PRIMARY KEY, user_id text NOT NULL REFERENCES users(id),
    purpose text NOT NULL, expires bigint NOT NULL, used boolean NOT NULL DEFAULT false);
CREATE TABLE dev_mailbox(
    user_id text NOT NULL REFERENCES users(id), purpose text NOT NULL, token text NOT NULL,
    expires bigint NOT NULL, PRIMARY KEY(user_id,purpose));
CREATE TABLE services(
    id text PRIMARY KEY, specialty text NOT NULL, name text NOT NULL,
    duration integer NOT NULL CHECK(duration BETWEEN 5 AND 240), active boolean NOT NULL DEFAULT true);
CREATE TABLE practitioners(id text PRIMARY KEY, name text NOT NULL, active boolean NOT NULL DEFAULT true);
CREATE TABLE slots(
    id text PRIMARY KEY, service_id text NOT NULL REFERENCES services(id),
    practitioner_id text NOT NULL REFERENCES practitioners(id),
    start_utc timestamptz NOT NULL, end_utc timestamptz NOT NULL, blocked boolean NOT NULL DEFAULT false,
    CHECK(start_utc < end_utc),
    UNIQUE(id,practitioner_id,start_utc,end_utc),
    EXCLUDE USING gist(practitioner_id WITH =, tstzrange(start_utc,end_utc,'[)') WITH &&));
CREATE TABLE appointments(
    id text PRIMARY KEY, user_id text NOT NULL REFERENCES users(id),
    slot_id text NOT NULL, practitioner_id text NOT NULL,
    start_utc timestamptz NOT NULL, end_utc timestamptz NOT NULL,
    FOREIGN KEY(slot_id,practitioner_id,start_utc,end_utc) REFERENCES slots(id,practitioner_id,start_utc,end_utc),
    status text NOT NULL CHECK(status IN ('PendingValidation','Confirmed','Cancelled','Rejected')),
    coverage text NOT NULL CHECK(coverage IN ('Pending','ManualReview','Approved','Denied','Expired')),
    insurer text NOT NULL, member_last4 text NOT NULL CHECK(member_last4 ~ '^[0-9]{4}$'),
    evidence text, coverage_valid_until timestamptz, reviewed_by text REFERENCES users(id), reviewed_at timestamptz,
    version integer NOT NULL DEFAULT 1,
    EXCLUDE USING gist(practitioner_id WITH =, tstzrange(start_utc,end_utc,'[)') WITH &&)
        WHERE(status IN ('PendingValidation','Confirmed')));
CREATE TABLE appointment_history(
    sequence bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    appointment_id text NOT NULL REFERENCES appointments(id), old_slot_id text, new_slot_id text NOT NULL,
    old_status text, new_status text NOT NULL, old_coverage text, new_coverage text NOT NULL,
    old_evidence text, new_evidence text, old_valid_until timestamptz, new_valid_until timestamptz,
    version integer NOT NULL, actor_id text NOT NULL, occurred_at timestamptz NOT NULL DEFAULT now());
CREATE FUNCTION track_appointment() RETURNS trigger LANGUAGE plpgsql AS $history$
BEGIN
    INSERT INTO appointment_history(appointment_id,old_slot_id,new_slot_id,old_status,new_status,
        old_coverage,new_coverage,old_evidence,new_evidence,old_valid_until,new_valid_until,version,actor_id)
    VALUES(NEW.id,CASE WHEN TG_OP='UPDATE' THEN OLD.slot_id ELSE NULL END,NEW.slot_id,
        CASE WHEN TG_OP='UPDATE' THEN OLD.status ELSE NULL END,NEW.status,
        CASE WHEN TG_OP='UPDATE' THEN OLD.coverage ELSE NULL END,NEW.coverage,
        CASE WHEN TG_OP='UPDATE' THEN OLD.evidence ELSE NULL END,NEW.evidence,
        CASE WHEN TG_OP='UPDATE' THEN OLD.coverage_valid_until ELSE NULL END,NEW.coverage_valid_until,NEW.version,
        coalesce(current_setting('telemedicina.actor',true),NEW.user_id));
    RETURN NEW;
END $history$;
CREATE TRIGGER appointment_history_written AFTER INSERT OR UPDATE ON appointments
    FOR EACH ROW EXECUTE FUNCTION track_appointment();
CREATE TABLE idempotency(
    user_id text NOT NULL REFERENCES users(id), key text NOT NULL, request_hash text NOT NULL,
    response text NOT NULL, PRIMARY KEY(user_id,key));
CREATE TABLE audit(
    sequence bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, id text NOT NULL UNIQUE,
    actor_id text NOT NULL, action text NOT NULL, resource_id text NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(), trace_id text NOT NULL);
CREATE FUNCTION protect_audit() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Audit events are append only'; END $body$;
CREATE TRIGGER audit_no_mutation BEFORE UPDATE OR DELETE OR TRUNCATE ON audit
    FOR EACH STATEMENT EXECUTE FUNCTION protect_audit();
CREATE TRIGGER history_no_mutation BEFORE UPDATE OR DELETE OR TRUNCATE ON appointment_history
    FOR EACH STATEMENT EXECUTE FUNCTION protect_audit();
INSERT INTO schema_versions(version) VALUES(1);
END IF;
END $migration$;
