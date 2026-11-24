# Diagrama de secuencia: iniciar y jugar una partida

Este diagrama presenta el flujo principal. Los dos participantes se representan como un solo actor porque ambos usan los mismos endpoints y reciben un estado privado equivalente. Las validaciones internas de mazo, Elo, ubicaciones y cartas se resumen en el servicio para mantener el diagrama legible.

```mermaid
sequenceDiagram
    autonumber

    actor J as Jugadores
    participant UI as React
    participant API as MatchmakingController
    participant S as MatchmakingService
    participant W as MatchmakingWorker
    participant R as Repositorio
    participant DB as PostgreSQL

    Note over J,DB: Buscar partida clasificatoria
        J->>UI: Selecciona un mazo y busca partida
        UI->>API: POST ranked con deckId y JWT
        API->>S: StartRankedAsync(playerId, deckId)
        S->>R: Validar jugador, mazo y saldo
        R->>DB: Leer datos y crear búsqueda
        DB-->>R: Búsqueda creada o error
        R-->>S: Resultado
        S-->>API: Estado Searching o error
        API-->>UI: Mostrar búsqueda o causa del rechazo
    Note over W,DB: Emparejamiento y preparación
        loop Cada 2 segundos
            W->>S: ProcessAsync()
            S->>R: Buscar jugadores compatibles
            R->>DB: Leer cola y catálogo de ubicaciones
            DB-->>R: Candidatos y ubicaciones
            alt Existe una pareja válida
                S->>S: Preparar partida y manos iniciales
                S->>R: Guardar partida y marcar búsquedas Matched
                R->>DB: Confirmar transacción
            else Aún no existe pareja válida
                S->>R: Mantener o expirar búsquedas
            end
        end
    Note over S,DB: Si la preparación falla no se crea una partida parcial

    Note over J,DB: Consultar y jugar la partida
        loop Mientras la partida esté activa
            UI->>API: GET current y GET state con JWT
            API->>S: CurrentAsync y MatchStateAsync
            S->>R: Leer búsqueda y estado privado del jugador
            R->>DB: Consultar partida y participantes
            DB-->>R: Estado persistido
            S-->>API: Estado visible para el jugador
            API-->>UI: Arena, mano, energía y reloj

            opt Juega o descarta una carta
                J->>UI: Elige la acción
                UI->>API: POST play o discard
                API->>S: Validar acción y actualizar estado
                S->>R: Guardar cambio
                R->>DB: Confirmar transacción
            end

            opt Finaliza el turno o vence el reloj
                UI->>API: POST turn advance
                API->>S: AdvanceMatchAsync
                alt Es el último turno
                    S->>R: Calcular resultado y cerrar partida
                else Hay otro turno
                    S->>R: Robar carta y preparar el siguiente turno
                end
                R->>DB: Confirmar estado actualizado
                S-->>API: Snapshot o resultado
                API-->>UI: Estado actualizado
            end
        end
```

## Alternativa: partida contra bot

Si una búsqueda clasificatoria activa ofrece un bot, el jugador llama `POST /api/player/matchmaking/{searchId}/bot`. `AcceptBotAsync` valida la oferta y usa la misma preparación de partida. Si tiene éxito, crea una partida con `IsBot=true`; si no, devuelve el estado o error correspondiente. El bot recibe mano, robos y energía, pero actualmente no juega cartas automáticamente.

## Notas de lectura

- Los endpoints requieren el JWT de la plataforma. El servidor obtiene el jugador, Elo y saldo sin confiar en esos datos enviados por el cliente.
- El estado de la partida es autoritativo en PostgreSQL. La API oculta la semilla, el orden del mazo y la mano rival.
- La mano inicial es de tres cartas y el turno 1 realiza el primer robo.
- El mismo proceso de preparación se reutiliza en el contexto de torneo mediante `StartAsync`; el controlador no expone un endpoint HTTP público para iniciar búsquedas de torneo.
