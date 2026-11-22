-- La cola comparte la reserva de jugador entre clasificatorias y futuros torneos.
BEGIN;

CREATE TABLE IF NOT EXISTS matches (
    id UUID PRIMARY KEY,
    context VARCHAR(20) NOT NULL CHECK (context IN ('Ranked','Tournament')),
    context_id UUID,
    status VARCHAR(20) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Completed','Cancelled')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK ((context='Ranked' AND context_id IS NULL) OR (context='Tournament' AND context_id IS NOT NULL))
);

CREATE TABLE IF NOT EXISTS match_players (
    match_id UUID NOT NULL REFERENCES matches(id),
    player_id UUID NOT NULL REFERENCES players(id),
    deck_id VARCHAR(100) NOT NULL,
    card_ids BIGINT[] NOT NULL CHECK (cardinality(card_ids)=12),
    elo INTEGER NOT NULL CHECK (elo>=0),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY(match_id,player_id)
);

-- Incluye todos los contextos: no puede haber dos reservas activas para el mismo jugador.
CREATE UNIQUE INDEX IF NOT EXISTS uq_match_player_active ON match_players(player_id) WHERE active;

CREATE TABLE IF NOT EXISTS matchmaking_searches (
    id UUID PRIMARY KEY,
    player_id UUID NOT NULL REFERENCES players(id),
    deck_id VARCHAR(100) NOT NULL,
    card_ids BIGINT[] NOT NULL CHECK (cardinality(card_ids)=12),
    context VARCHAR(20) NOT NULL CHECK (context IN ('Ranked','Tournament')),
    context_id UUID,
    elo INTEGER NOT NULL CHECK (elo>=0),
    status VARCHAR(20) NOT NULL DEFAULT 'Searching' CHECK (status IN ('Searching','Matched','Cancelled')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    match_id UUID REFERENCES matches(id),
    last_checked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    end_reason TEXT,
    CHECK ((context='Ranked' AND context_id IS NULL) OR (context='Tournament' AND context_id IS NOT NULL)),
    CHECK ((status='Matched' AND match_id IS NOT NULL) OR (status<>'Matched' AND match_id IS NULL))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_matchmaking_open_player
    ON matchmaking_searches(player_id) WHERE status='Searching';
CREATE INDEX IF NOT EXISTS idx_matchmaking_candidates
    ON matchmaking_searches(context,context_id,elo,created_at,id) WHERE status='Searching';
CREATE INDEX IF NOT EXISTS idx_matchmaking_player_history
    ON matchmaking_searches(player_id,created_at DESC,id);
CREATE INDEX IF NOT EXISTS idx_matchmaking_processing
    ON matchmaking_searches(last_checked_at,created_at,id) WHERE status='Searching';

COMMIT;
