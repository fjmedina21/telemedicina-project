-- H04-SPEC-v1. Explicit, additive and repeatable; preserves slot data.
SET LOCAL search_path TO telemedicina_m1, public;
ALTER TABLE slots ADD COLUMN IF NOT EXISTS version integer NOT NULL DEFAULT 1;
INSERT INTO schema_versions(version) VALUES(7) ON CONFLICT(version) DO NOTHING;
