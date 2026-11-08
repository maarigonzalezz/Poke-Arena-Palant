-- Registra el registro inicial sin modificar saldos ni inventarios existentes.
BEGIN;

CREATE TABLE IF NOT EXISTS player_cube_movements (
    player_id UUID NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    sequence INTEGER NOT NULL CHECK (sequence > 0),
    amount INTEGER NOT NULL CHECK (amount <> 0),
    balance_after INTEGER NOT NULL CHECK (balance_after >= 0),
    reason VARCHAR(50) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (player_id, sequence),
    CONSTRAINT chk_initial_cube_movement CHECK (
        reason <> 'InitialCollection' OR (sequence = 1 AND amount = 1000 AND balance_after = 1000)
    )
);

-- El primer registro también impide volver a entregar el premio aunque el saldo llegue a cero.
CREATE UNIQUE INDEX IF NOT EXISTS uq_player_initial_collection
    ON player_cube_movements(player_id) WHERE reason = 'InitialCollection';

COMMIT;
