-- Restore future history writes without reconstructing historical events.
SET LOCAL search_path TO telemedicina_m1, public;
CREATE OR REPLACE FUNCTION telemedicina_m1.track_appointment() RETURNS trigger LANGUAGE plpgsql AS $history$
BEGIN
    INSERT INTO telemedicina_m1.appointment_history(appointment_id,old_slot_id,new_slot_id,old_status,new_status,
        old_coverage,new_coverage,old_evidence,new_evidence,old_valid_until,new_valid_until,version,actor_id)
    VALUES(NEW.id,CASE WHEN TG_OP='UPDATE' THEN OLD.slot_id ELSE NULL END,NEW.slot_id,
        CASE WHEN TG_OP='UPDATE' THEN OLD.status ELSE NULL END,NEW.status,
        CASE WHEN TG_OP='UPDATE' THEN OLD.coverage ELSE NULL END,NEW.coverage,
        CASE WHEN TG_OP='UPDATE' THEN OLD.evidence ELSE NULL END,NEW.evidence,
        CASE WHEN TG_OP='UPDATE' THEN OLD.coverage_valid_until ELSE NULL END,NEW.coverage_valid_until,NEW.version,
        coalesce(current_setting('telemedicina.actor',true),NEW.user_id));
    RETURN NEW;
END $history$;
DROP TRIGGER IF EXISTS appointment_history_written ON telemedicina_m1.appointments;
CREATE TRIGGER appointment_history_written AFTER INSERT OR UPDATE ON telemedicina_m1.appointments
    FOR EACH ROW EXECUTE FUNCTION telemedicina_m1.track_appointment();
INSERT INTO schema_versions(version) VALUES(8) ON CONFLICT(version) DO NOTHING;
