-- H03-SPEC-v1. Explicit additive migration; unknown historical dates stay NULL.
SET LOCAL search_path TO telemedicina_m1, public;
ALTER TABLE profiles ADD COLUMN IF NOT EXISTS allergies_updated_at timestamptz;
ALTER TABLE profiles ADD COLUMN IF NOT EXISTS medications_updated_at timestamptz;
ALTER TABLE profiles ADD COLUMN IF NOT EXISTS conditions_updated_at timestamptz;
INSERT INTO schema_versions(version) VALUES(6) ON CONFLICT(version) DO NOTHING;
