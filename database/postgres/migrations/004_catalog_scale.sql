-- Prepara búsquedas parciales y contadores amplios sin renumerar cartas existentes.
BEGIN;

CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

ALTER TABLE cards ADD COLUMN IF NOT EXISTS search_name TEXT NOT NULL DEFAULT '';

-- Un trigger permite normalizar nombres sin declarar unaccent como inmutable.
CREATE OR REPLACE FUNCTION normalize_card_search_name() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    NEW.search_name := lower(unaccent(NEW.name));
    RETURN NEW;
END;
$$;
DROP TRIGGER IF EXISTS card_search_name ON cards;
CREATE TRIGGER card_search_name BEFORE INSERT OR UPDATE OF name, search_name ON cards
    FOR EACH ROW EXECUTE FUNCTION normalize_card_search_name();

-- Completa también las cartas anteriores a esta migración.
UPDATE cards SET search_name = lower(unaccent(name))
WHERE search_name IS DISTINCT FROM lower(unaccent(name));

CREATE INDEX IF NOT EXISTS idx_cards_search_name_trgm
    ON cards USING gin (search_name gin_trgm_ops) WHERE active;

-- El contador INTEGER ya admite más de 999; se considera el sufijo completo.
UPDATE card_code_sequences s SET last_number = GREATEST(s.last_number,
    COALESCE((SELECT LEAST(COALESCE(MAX(substring(c.code FROM 9)::bigint), 0), 2147483647)::integer
              FROM cards c WHERE c.card_type = s.card_type
              AND c.code ~ '^PKA-(FUE|AGU|PLA|ELE|PSI|LUC)-[0-9]{3,}$'), 0));

COMMIT;
ANALYZE cards;
