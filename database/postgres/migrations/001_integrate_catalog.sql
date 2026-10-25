-- Ejecutar una vez sobre bases existentes; también se ejecuta al inicializar Docker.
BEGIN;

-- Conserva las habilidades ya definidas por el equipo.
INSERT INTO abilities(code,name,trigger_type,effect_type,parameters,restricted_card_type)
VALUES ('HAB-001','Ataque ígneo','AlRevelarse','AumentarPoder','{"power":3}','Fuego'),
       ('HAB-002','Escudo acuático','Continuo','AumentarPoder','{"power":2}','Agua')
ON CONFLICT (code) DO NOTHING;

-- Reconcilia el contador con códigos cargados antes de integrar la creación.
UPDATE card_code_sequences s SET last_number = GREATEST(s.last_number,
    COALESCE((SELECT MAX(RIGHT(c.code,3)::integer) FROM cards c
              WHERE c.card_type=s.card_type AND c.code ~ '^PKA-(FUE|AGU|PLA|ELE|PSI|LUC)-[0-9]{3}$'),0));

CREATE OR REPLACE FUNCTION preserve_card_code() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.code IS DISTINCT FROM OLD.code THEN
        RAISE EXCEPTION 'El código de una carta es inmutable.' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
DROP TRIGGER IF EXISTS card_code_immutable ON cards;
CREATE TRIGGER card_code_immutable BEFORE UPDATE OF code ON cards
    FOR EACH ROW EXECUTE FUNCTION preserve_card_code();

-- Los consumidores de partidas deben consultar estas vistas, nunca todas las filas activas.
CREATE OR REPLACE VIEW playable_cards AS
SELECT * FROM cards WHERE active AND image_status='Ready'
    AND image_original_path IS NOT NULL AND image_small_path IS NOT NULL;
CREATE OR REPLACE VIEW available_locations AS
SELECT * FROM locations WHERE active AND image_status='Ready'
    AND image_original_path IS NOT NULL AND image_small_path IS NOT NULL;

COMMIT;
