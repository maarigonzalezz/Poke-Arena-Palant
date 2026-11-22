-- Agrega cuentas externas sin modificar cartas, ubicaciones ni búsquedas existentes.
BEGIN;

CREATE TABLE IF NOT EXISTS players (
    id UUID PRIMARY KEY,
    identity_provider VARCHAR(20) NOT NULL,
    provider_subject VARCHAR(255) NOT NULL,
    email TEXT,
    display_name TEXT,
    role VARCHAR(20) NOT NULL DEFAULT 'Player',
    elo INTEGER NOT NULL DEFAULT 0,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_login_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_players_identity UNIQUE (identity_provider, provider_subject),
    CONSTRAINT chk_player_identity CHECK (identity_provider <> '' AND provider_subject <> ''),
    CONSTRAINT chk_player_role CHECK (role IN ('Player', 'ContentAdmin')),
    CONSTRAINT chk_player_elo CHECK (elo >= 0)
);

-- El correo no es único: cuentas externas distintas nunca se fusionan por correo.
COMMIT;
