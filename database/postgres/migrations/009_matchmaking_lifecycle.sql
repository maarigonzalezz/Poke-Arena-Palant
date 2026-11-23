-- Amplía una instalación existente sin recrear la cola ni sus partidas.
BEGIN;

ALTER TABLE matchmaking_searches ADD COLUMN IF NOT EXISTS last_polled_at TIMESTAMPTZ;
-- Las búsquedas anteriores reciben un plazo inicial al instalar esta migración.
UPDATE matchmaking_searches SET last_polled_at=NOW() WHERE last_polled_at IS NULL;
ALTER TABLE matchmaking_searches ALTER COLUMN last_polled_at SET NOT NULL;
ALTER TABLE matchmaking_searches ALTER COLUMN last_polled_at SET DEFAULT NOW();
ALTER TABLE matchmaking_searches DROP CONSTRAINT IF EXISTS matchmaking_searches_status_check;
ALTER TABLE matchmaking_searches ADD CONSTRAINT matchmaking_searches_status_check
    CHECK (status IN ('Searching','Matched','Cancelled','Expired'));

ALTER TABLE matches ADD COLUMN IF NOT EXISTS is_bot BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE matches ADD COLUMN IF NOT EXISTS bot_card_ids BIGINT[];
ALTER TABLE matches DROP CONSTRAINT IF EXISTS ck_matches_bot_deck;
ALTER TABLE matches ADD CONSTRAINT ck_matches_bot_deck CHECK (
    (NOT is_bot AND bot_card_ids IS NULL)
    OR (is_bot AND context='Ranked' AND bot_card_ids IS NOT NULL AND cardinality(bot_card_ids)=12)
);

CREATE INDEX IF NOT EXISTS idx_matchmaking_inactivity
    ON matchmaking_searches(last_polled_at,id) WHERE status='Searching';

COMMIT;
