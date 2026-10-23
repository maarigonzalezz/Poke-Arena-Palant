
-- Esquema inicial: Docker lo ejecuta al crear un volumen PostgreSQL vacío.
CREATE EXTENSION IF NOT EXISTS unaccent;

-- ============================================
-- HABILIDADES
-- ============================================

CREATE TABLE abilities (
    code VARCHAR(20) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    trigger_type VARCHAR(50),
    effect_type VARCHAR(50),
    parameters JSONB NOT NULL DEFAULT '{}',
    restricted_card_type VARCHAR(20),
    active BOOLEAN NOT NULL DEFAULT TRUE
);

INSERT INTO abilities (
    code,
    name,
    trigger_type,
    effect_type,
    parameters
)
VALUES (
    'HAB-000',
    'Sin Habilidad',
    NULL,
    NULL,
    '{}'
)
ON CONFLICT (code) DO NOTHING;

-- ============================================
-- CONTADORES DE CÓDIGOS DE CARTAS
-- ============================================

CREATE TABLE card_code_sequences (
    card_type VARCHAR(20) PRIMARY KEY,
    last_number INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT chk_sequence_nonnegative
        CHECK (last_number >= 0)
);

INSERT INTO card_code_sequences (
    card_type,
    last_number
)
VALUES
    ('Fuego', 0),
    ('Agua', 0),
    ('Planta', 0),
    ('Eléctrico', 0),
    ('Psíquico', 0),
    ('Lucha', 0)
ON CONFLICT DO NOTHING;

-- ============================================
-- CARTAS
-- ============================================

CREATE TABLE cards (
    id BIGINT GENERATED ALWAYS AS IDENTITY
        PRIMARY KEY,

    code VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(120) NOT NULL,

    cost INTEGER NOT NULL,
    power INTEGER NOT NULL,

    card_type VARCHAR(20) NOT NULL,
    rarity VARCHAR(20) NOT NULL,

    ability_code VARCHAR(20) NOT NULL
        REFERENCES abilities(code),

    image_status VARCHAR(20) NOT NULL
        DEFAULT 'Pending',

    image_original_path TEXT,
    image_small_path TEXT,

    current_version INTEGER NOT NULL DEFAULT 1,
    active BOOLEAN NOT NULL DEFAULT TRUE,

    created_at TIMESTAMPTZ NOT NULL
        DEFAULT NOW(),

    CONSTRAINT chk_card_cost
        CHECK (cost BETWEEN 0 AND 10),

    CONSTRAINT chk_card_power
        CHECK (power BETWEEN -20 AND 20),

    CONSTRAINT chk_card_type
        CHECK (
            card_type IN (
                'Fuego',
                'Agua',
                'Planta',
                'Eléctrico',
                'Psíquico',
                'Lucha'
            )
        ),

    CONSTRAINT chk_card_rarity
        CHECK (
            rarity IN (
                'Común',
                'Poco común',
                'Rara',
                'Legendaria'
            )
        ),

    CONSTRAINT chk_card_image_status
        CHECK (
            image_status IN (
                'Pending',
                'Processing',
                'Ready',
                'Failed'
            )
        )
);

-- Versiones inmutables para partidas e historial.
CREATE TABLE card_versions (
    card_id BIGINT NOT NULL
        REFERENCES cards(id),

    version INTEGER NOT NULL,

    snapshot JSONB NOT NULL,

    created_at TIMESTAMPTZ NOT NULL
        DEFAULT NOW(),

    PRIMARY KEY (card_id, version)
);

-- ============================================
-- UBICACIONES
-- ============================================

CREATE TABLE locations (
    id BIGINT GENERATED ALWAYS AS IDENTITY
        PRIMARY KEY,

    name VARCHAR(120) NOT NULL,
    description TEXT NOT NULL,

    max_cards INTEGER NOT NULL,

    energy_mode VARCHAR(20) NOT NULL,
    extra_energy INTEGER NOT NULL,

    effect_type VARCHAR(50) NOT NULL
        DEFAULT 'None',

    effect_parameters JSONB NOT NULL
        DEFAULT '{}',

    image_status VARCHAR(20) NOT NULL
        DEFAULT 'Pending',

    image_original_path TEXT,
    image_small_path TEXT,

    current_version INTEGER NOT NULL DEFAULT 1,
    active BOOLEAN NOT NULL DEFAULT TRUE,

    created_at TIMESTAMPTZ NOT NULL
        DEFAULT NOW(),

    CONSTRAINT chk_location_cards
        CHECK (max_cards BETWEEN 0 AND 5),

    CONSTRAINT chk_energy_mode
        CHECK (
            energy_mode IN (
                'Fixed',
                'PerCard'
            )
        ),

    CONSTRAINT chk_energy_amount
        CHECK (
            (
                energy_mode = 'Fixed'
                AND extra_energy BETWEEN 0 AND 3
            )
            OR
            (
                energy_mode = 'PerCard'
                AND extra_energy BETWEEN 0 AND 2
            )
        ),

    CONSTRAINT chk_effect_type
        CHECK (
            effect_type IN (
                'None',
                'TypeBonus',
                'CostModifier',
                'TurnRestriction'
            )
        )
);

CREATE TABLE location_versions (
    location_id BIGINT NOT NULL
        REFERENCES locations(id),

    version INTEGER NOT NULL,

    snapshot JSONB NOT NULL,

    created_at TIMESTAMPTZ NOT NULL
        DEFAULT NOW(),

    PRIMARY KEY (location_id, version)
);

-- ============================================
-- IDEMPOTENCIA
-- ============================================

-- Reserva claves por usuario y operación para registrar respuestas de reintentos.
CREATE TABLE idempotency_requests (
    id BIGINT GENERATED ALWAYS AS IDENTITY
        PRIMARY KEY,

    actor_id VARCHAR(100) NOT NULL,
    operation VARCHAR(100) NOT NULL,
    idempotency_key VARCHAR(100) NOT NULL,

    request_hash VARCHAR(128) NOT NULL,

    status VARCHAR(20) NOT NULL,

    response_status INTEGER,
    response_body JSONB,

    created_at TIMESTAMPTZ NOT NULL
        DEFAULT NOW(),

    CONSTRAINT uq_idempotency
        UNIQUE (
            actor_id,
            operation,
            idempotency_key
        ),

    CONSTRAINT chk_idempotency_status
        CHECK (
            status IN (
                'Processing',
                'Completed'
            )
        )
);

-- ============================================
-- OUTBOX TRANSACCIONAL
-- ============================================

-- Almacena eventos pendientes; este esquema no implementa su procesamiento.
CREATE TABLE outbox_events (
    id UUID PRIMARY KEY,

    event_type VARCHAR(100) NOT NULL,
    aggregate_type VARCHAR(50) NOT NULL,
    aggregate_id BIGINT NOT NULL,

    payload JSONB NOT NULL,

    created_at TIMESTAMPTZ NOT NULL
        DEFAULT NOW(),

    processed_at TIMESTAMPTZ,
    attempts INTEGER NOT NULL DEFAULT 0
);

-- ============================================
-- ÍNDICES DE CONSULTAS
-- ============================================

CREATE INDEX idx_cards_name
    ON cards (name);

CREATE INDEX idx_cards_type
    ON cards (card_type, id);

CREATE INDEX idx_cards_rarity
    ON cards (rarity, id);

CREATE INDEX idx_cards_cost
    ON cards (cost, id);

CREATE INDEX idx_cards_power
    ON cards (power, id);

CREATE INDEX idx_cards_ability
    ON cards (ability_code, id);

CREATE INDEX idx_cards_created
    ON cards (created_at, id);

CREATE INDEX idx_outbox_pending
    ON outbox_events (created_at, id)
    WHERE processed_at IS NULL;


-- ============================================
-- SESIONES DE BÚSQUEDA
-- ============================================

CREATE TABLE card_search_sessions (
    id UUID PRIMARY KEY,
    actor_id VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    result_count BIGINT NOT NULL DEFAULT 0
);

-- Conserva identificadores y posiciones, no una copia de los atributos de cada carta.
CREATE TABLE card_search_items (
    session_id UUID NOT NULL
        REFERENCES card_search_sessions(id)
        ON DELETE CASCADE,

    position BIGINT NOT NULL,
    card_id BIGINT NOT NULL
        REFERENCES cards(id),

    PRIMARY KEY (session_id, position),

    UNIQUE (session_id, card_id)
);

CREATE INDEX idx_card_search_expiration
    ON card_search_sessions(expires_at);
