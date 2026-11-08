-- Agrega relaciones para el inventario de los jugadores: incluye sus cartas y sus monedas virtuales.
BEGIN;

-- Coleccion de cartas de cada jugador: no se permite duplicar la misma carta en el inventario.
CREATE TABLE IF NOT EXISTS player_cards (
    player_id UUID NOT NULL,
    card_id BIGINT NOT NULL,

    PRIMARY KEY (player_id, card_id),

    FOREIGN KEY (player_id) 
        REFERENCES players(id) 
        ON DELETE CASCADE,

    FOREIGN KEY (card_id) 
        REFERENCES cards(id) 
        ON DELETE CASCADE
);

-- Agregar la tabla de monedas virtuales de los jugadores
ALTER TABLE players
    ADD COLUMN IF NOT EXISTS cubes INTEGER NOT NULL DEFAULT 0;

END;