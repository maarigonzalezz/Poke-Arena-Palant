BEGIN;

CREATE TABLE IF NOT EXISTS player_decks (
    id UUID PRIMARY KEY,
    player_id UUID NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    name VARCHAR(40) NOT NULL CHECK (name <> '' AND name = btrim(name)),
    name_key TEXT NOT NULL CHECK (name_key <> ''),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_player_decks_name
    ON player_decks(player_id, name_key);

CREATE TABLE IF NOT EXISTS player_deck_cards (
    deck_id UUID NOT NULL REFERENCES player_decks(id) ON DELETE CASCADE,
    card_id BIGINT NOT NULL REFERENCES cards(id) ON DELETE RESTRICT,
    PRIMARY KEY (deck_id, card_id)
);

CREATE INDEX IF NOT EXISTS idx_player_deck_cards_card ON player_deck_cards(card_id);

CREATE OR REPLACE VIEW public.matchmaking_deck_source AS
    SELECT deck.id::text AS deck_id, deck.player_id, deck.name::text AS name, entry.card_id
    FROM player_decks deck
    LEFT JOIN player_deck_cards entry ON entry.deck_id = deck.id;

COMMIT;
