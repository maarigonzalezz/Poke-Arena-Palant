BEGIN;

CREATE TABLE IF NOT EXISTS player_decks (
    id VARCHAR(100) PRIMARY KEY,
    player_id UUID NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    name VARCHAR(80) NOT NULL,
    card_ids BIGINT[] NOT NULL CHECK (cardinality(card_ids)=12),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_player_decks_owner
    ON player_decks(player_id, created_at DESC, id);

CREATE OR REPLACE VIEW matchmaking_deck_source AS
SELECT deck.id AS deck_id, deck.player_id, deck.name, cards.card_id
FROM player_decks deck
CROSS JOIN LATERAL unnest(deck.card_ids) AS cards(card_id);

COMMIT;
