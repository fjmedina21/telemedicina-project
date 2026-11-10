SET LOCAL search_path TO telemedicina_m1, public;
DO $migration$
BEGIN
IF NOT EXISTS (SELECT 1 FROM schema_versions WHERE version=11) THEN
    CREATE TABLE video_sessions(
        id text PRIMARY KEY, appointment_id text NOT NULL REFERENCES appointments(id),
        provider text NOT NULL CHECK(provider='livekit'), provider_room_id text NOT NULL UNIQUE,
        status text NOT NULL CHECK(status IN ('Created','Waiting','Active','Interrupted','Ended','Failed')),
        connected_participants integer NOT NULL DEFAULT 0 CHECK(connected_participants BETWEEN 0 AND 2),
        created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
        started_at timestamptz, ended_at timestamptz);
    CREATE UNIQUE INDEX video_one_open_session ON video_sessions(appointment_id)
        WHERE status NOT IN ('Ended','Failed');
    CREATE INDEX video_sessions_appointment ON video_sessions(appointment_id,status);
    CREATE TABLE video_connection_events(
        id text PRIMARY KEY, session_id text NOT NULL REFERENCES video_sessions(id),
        actor_id text NOT NULL REFERENCES users(id), event_type text NOT NULL,
        occurred_at timestamptz NOT NULL DEFAULT now(),
        CHECK(event_type IN ('connecting','connected','reconnecting','reconnected','disconnected','failed')));
    INSERT INTO schema_versions(version) VALUES(11);
END IF;
END $migration$;
