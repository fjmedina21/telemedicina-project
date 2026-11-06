-- Restore the two append-only guards that were missing from the development schema.
-- Do not drop or replace unexpected trigger definitions.
SET LOCAL search_path TO telemedicina_m1, public;

DO $guards$
DECLARE
    protect_function oid := to_regprocedure('telemedicina_m1.protect_audit()')::oid;
BEGIN
    IF protect_function IS NULL THEN
        RAISE EXCEPTION 'Required function telemedicina_m1.protect_audit() is missing';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_trigger g
        JOIN pg_class r ON r.oid=g.tgrelid
        JOIN pg_namespace n ON n.oid=r.relnamespace
        WHERE n.nspname='telemedicina_m1' AND g.tgname='audit_no_mutation'
          AND NOT (r.relname='audit' AND g.tgfoid=protect_function AND g.tgtype=58
                   AND g.tgenabled='O' AND g.tgnargs=0 AND g.tgqual IS NULL AND NOT g.tgisinternal)
    ) THEN
        RAISE EXCEPTION 'Unexpected definition for telemedicina_m1.audit_no_mutation';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger g
        JOIN pg_class r ON r.oid=g.tgrelid
        JOIN pg_namespace n ON n.oid=r.relnamespace
        WHERE n.nspname='telemedicina_m1' AND r.relname='audit' AND g.tgname='audit_no_mutation'
          AND g.tgfoid=protect_function AND g.tgtype=58 AND g.tgenabled='O'
          AND g.tgnargs=0 AND g.tgqual IS NULL AND NOT g.tgisinternal
    ) THEN
        EXECUTE 'CREATE TRIGGER audit_no_mutation BEFORE UPDATE OR DELETE OR TRUNCATE ON telemedicina_m1.audit '
             || 'FOR EACH STATEMENT EXECUTE FUNCTION telemedicina_m1.protect_audit()';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_trigger g
        JOIN pg_class r ON r.oid=g.tgrelid
        JOIN pg_namespace n ON n.oid=r.relnamespace
        WHERE n.nspname='telemedicina_m1' AND g.tgname='history_no_mutation'
          AND NOT (r.relname='appointment_history' AND g.tgfoid=protect_function AND g.tgtype=58
                   AND g.tgenabled='O' AND g.tgnargs=0 AND g.tgqual IS NULL AND NOT g.tgisinternal)
    ) THEN
        RAISE EXCEPTION 'Unexpected definition for telemedicina_m1.history_no_mutation';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger g
        JOIN pg_class r ON r.oid=g.tgrelid
        JOIN pg_namespace n ON n.oid=r.relnamespace
        WHERE n.nspname='telemedicina_m1' AND r.relname='appointment_history'
          AND g.tgname='history_no_mutation' AND g.tgfoid=protect_function AND g.tgtype=58
          AND g.tgenabled='O' AND g.tgnargs=0 AND g.tgqual IS NULL AND NOT g.tgisinternal
    ) THEN
        EXECUTE 'CREATE TRIGGER history_no_mutation BEFORE UPDATE OR DELETE OR TRUNCATE ON telemedicina_m1.appointment_history '
             || 'FOR EACH STATEMENT EXECUTE FUNCTION telemedicina_m1.protect_audit()';
    END IF;
END
$guards$;

INSERT INTO telemedicina_m1.schema_versions(version) VALUES(9)
ON CONFLICT(version) DO NOTHING;
