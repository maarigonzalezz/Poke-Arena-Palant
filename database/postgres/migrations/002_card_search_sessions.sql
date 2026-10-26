-- Completa bases existentes sin modificar las cartas ni las búsquedas guardadas.
BEGIN;

CREATE EXTENSION IF NOT EXISTS unaccent;

CREATE TABLE IF NOT EXISTS card_search_sessions (
    id UUID PRIMARY KEY,
    actor_id VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    result_count BIGINT NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS card_search_items (
    session_id UUID NOT NULL REFERENCES card_search_sessions(id) ON DELETE CASCADE,
    position BIGINT NOT NULL,
    card_id BIGINT NOT NULL REFERENCES cards(id),
    PRIMARY KEY (session_id, position),
    UNIQUE (session_id, card_id)
);

CREATE INDEX IF NOT EXISTS idx_card_search_expiration
    ON card_search_sessions(expires_at);

COMMIT;
