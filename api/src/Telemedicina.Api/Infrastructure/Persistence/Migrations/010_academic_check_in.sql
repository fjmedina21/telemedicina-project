-- H06 academic demo only. Administrative declarations; never clinical data.
SET LOCAL search_path TO telemedicina_m1, public;

DO $migration$
BEGIN
IF NOT EXISTS (SELECT 1 FROM schema_versions WHERE version=10) THEN
    CREATE TABLE check_in_attempts(
        id text PRIMARY KEY,
        appointment_id text NOT NULL REFERENCES appointments(id),
        user_id text NOT NULL REFERENCES users(id),
        appointment_version integer NOT NULL CHECK(appointment_version > 0),
        profile_version integer NOT NULL CHECK(profile_version > 0),
        state text NOT NULL CHECK(state IN
            ('NotStarted','InProgress','Verified','Ready','Rejected','Expired')),
        prepared boolean NOT NULL DEFAULT false,
        privacy_confirmed boolean NOT NULL DEFAULT false,
        connectivity_confirmed boolean NOT NULL DEFAULT false,
        technical_preflight_completed boolean NOT NULL DEFAULT false,
        consent_version integer,
        consent_hash text,
        version integer NOT NULL DEFAULT 1 CHECK(version > 0),
        active boolean NOT NULL DEFAULT true,
        created_at timestamptz NOT NULL DEFAULT now(),
        updated_at timestamptz NOT NULL DEFAULT now(),
        ready_at timestamptz,
        invalidated_at timestamptz,
        CHECK((consent_version IS NULL) = (consent_hash IS NULL)),
        CHECK(consent_version IS NULL OR consent_version = 1),
        CHECK(consent_hash IS NULL OR consent_hash =
            'a7ae0d686693ad80daaef8b775d47b062484ad354ecb17a90df10ac865626631'),
        CHECK(state<>'Ready' OR (active AND prepared AND privacy_confirmed
            AND connectivity_confirmed AND technical_preflight_completed
            AND consent_version=1 AND consent_hash=
                'a7ae0d686693ad80daaef8b775d47b062484ad354ecb17a90df10ac865626631'
            AND ready_at IS NOT NULL)),
        CHECK(state='Ready' OR ready_at IS NULL)
    );
    CREATE UNIQUE INDEX check_in_one_active_attempt
        ON check_in_attempts(appointment_id) WHERE active;
    CREATE INDEX check_in_ready_lookup
        ON check_in_attempts(appointment_id,state) WHERE active;
    -- H07 must consume this projection, not the base table. It exposes only
    -- current rows created through the H06 command invariants above.
    CREATE VIEW ready_check_ins AS
        SELECT ci.appointment_id,ci.user_id,ci.ready_at
        FROM check_in_attempts ci
        JOIN appointments a ON a.id=ci.appointment_id
        JOIN profiles p ON p.user_id=ci.user_id AND p.version=ci.profile_version
        WHERE ci.active AND ci.state='Ready' AND ci.prepared
          AND ci.privacy_confirmed AND ci.connectivity_confirmed
          AND ci.technical_preflight_completed
          AND ci.consent_version=1 AND ci.consent_hash=
              'a7ae0d686693ad80daaef8b775d47b062484ad354ecb17a90df10ac865626631'
          AND ci.ready_at IS NOT NULL
          AND ci.appointment_version=a.version
          AND a.status='Confirmed'
          AND now() BETWEEN a.start_utc-interval '30 minutes'
                        AND a.start_utc+interval '15 minutes';
    INSERT INTO schema_versions(version) VALUES(10);
END IF;
END
$migration$;
