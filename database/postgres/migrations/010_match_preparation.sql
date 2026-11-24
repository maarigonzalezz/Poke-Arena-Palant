BEGIN;

ALTER TABLE matches
    ADD COLUMN IF NOT EXISTS preparation_seed BIGINT,
    ADD COLUMN IF NOT EXISTS turn_number INTEGER NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS turn_started_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS turn_ends_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS wager_cubes INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS bot_hand_card_ids BIGINT[],
    ADD COLUMN IF NOT EXISTS bot_cemetery_card_ids BIGINT[] NOT NULL DEFAULT ARRAY[]::BIGINT[],
    ADD COLUMN IF NOT EXISTS bot_energy INTEGER NOT NULL DEFAULT 0;

ALTER TABLE match_players
    ADD COLUMN IF NOT EXISTS hand_card_ids BIGINT[] NOT NULL DEFAULT ARRAY[]::BIGINT[],
    ADD COLUMN IF NOT EXISTS cemetery_card_ids BIGINT[] NOT NULL DEFAULT ARRAY[]::BIGINT[],
    ADD COLUMN IF NOT EXISTS remaining_card_ids BIGINT[] NOT NULL DEFAULT ARRAY[]::BIGINT[],
    ADD COLUMN IF NOT EXISTS energy INTEGER NOT NULL DEFAULT 2;

UPDATE match_players AS players
SET hand_card_ids = players.card_ids[1:3]
FROM matches AS matches
WHERE matches.id=players.match_id AND matches.turn_started_at IS NULL
  AND cardinality(players.hand_card_ids)=0 AND cardinality(players.card_ids)>=3;

UPDATE match_players AS players
SET remaining_card_ids = players.card_ids[4:cardinality(players.card_ids)]
FROM matches AS matches
WHERE matches.id=players.match_id AND matches.turn_started_at IS NULL
  AND cardinality(players.card_ids)>=3;

ALTER TABLE match_players DROP CONSTRAINT IF EXISTS ck_match_player_opening_hand;
ALTER TABLE match_players ADD CONSTRAINT ck_match_player_opening_hand
    CHECK (cardinality(hand_card_ids) BETWEEN 0 AND 7);

ALTER TABLE match_players DROP CONSTRAINT IF EXISTS ck_match_player_cemetery;
ALTER TABLE match_players ADD CONSTRAINT ck_match_player_cemetery
    CHECK (cardinality(cemetery_card_ids)>=0);

ALTER TABLE match_players DROP CONSTRAINT IF EXISTS ck_match_player_energy;
ALTER TABLE match_players ADD CONSTRAINT ck_match_player_energy
    CHECK (energy>=0);

CREATE TABLE IF NOT EXISTS match_location_choices (
    match_id UUID NOT NULL REFERENCES matches(id) ON DELETE CASCADE,
    slot INTEGER NOT NULL CHECK (slot BETWEEN 1 AND 3),
    location_id BIGINT NOT NULL REFERENCES locations(id),
    snapshot JSONB NOT NULL,
    PRIMARY KEY(match_id,slot),
    UNIQUE(match_id,location_id)
);

CREATE INDEX IF NOT EXISTS idx_match_location_choices_location
    ON match_location_choices(location_id);

ALTER TABLE matches DROP CONSTRAINT IF EXISTS ck_match_preparation_turn;
ALTER TABLE matches ADD CONSTRAINT ck_match_preparation_turn
    CHECK (turn_number>=1 AND turn_number<=6);

COMMIT;
