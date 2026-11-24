BEGIN;

ALTER TABLE match_players
    ADD COLUMN IF NOT EXISTS played_card_ids BIGINT[] NOT NULL DEFAULT ARRAY[]::BIGINT[],
    ADD COLUMN IF NOT EXISTS played_location_slots INTEGER[] NOT NULL DEFAULT ARRAY[]::INTEGER[];

ALTER TABLE match_players DROP CONSTRAINT IF EXISTS ck_match_player_opening_hand;
ALTER TABLE match_players ADD CONSTRAINT ck_match_player_opening_hand
    CHECK (cardinality(hand_card_ids) BETWEEN 0 AND 7);

ALTER TABLE match_players DROP CONSTRAINT IF EXISTS ck_match_player_placed_cards;
ALTER TABLE match_players ADD CONSTRAINT ck_match_player_placed_cards
    CHECK (cardinality(played_card_ids)=cardinality(played_location_slots));

ALTER TABLE match_players DROP CONSTRAINT IF EXISTS ck_match_player_card_locations;
ALTER TABLE match_players ADD CONSTRAINT ck_match_player_card_locations
    CHECK (played_location_slots <@ ARRAY[1,2,3]::INTEGER[]);

COMMIT;
