# Estado, cancelación y oferta del bot

La lógica está en `MatchmakingService`; los repositories consultan y persisten. Todas las operaciones comparten la transacción y el bloqueo de PostgreSQL ya usados para emparejar entre instancias.

## Política acordada

- `Searching`: en espera. Solo `GET /current` renueva `LastPolledAt`; el worker no representa presencia del jugador.
- `Matched`: partida asignada, con `MatchId`. La salida devuelve `409` con ese identificador.
- `Cancelled`: salida confirmada o pérdida de las condiciones para jugar.
- `Expired`: 60 segundos sin consultar. Una consulta tardía informa este estado y no reactiva la búsqueda.

`CreatedAt` mide la espera total, `LastPolledAt` mide la presencia y `LastCheckedAt` permite rotar los lotes del worker. Los plazos están en `MatchmakingRules`.

Tras 60 segundos de espera, una búsqueda clasificatoria vigente devuelve `botAvailable: true`. Consultar no reinicia esa espera. Ignorar la oferta mantiene la búsqueda humana y la oferta disponibles; no hace falta enviar un rechazo.

Aceptar crea una partida activa con `is_bot=true`, una participación humana y el deck original reservado. Por ahora, el bot utiliza una copia de esas mismas 12 cartas, guardada en `matches.bot_card_ids`; no se inventa una cuenta de jugador. Esta historia registra el encuentro; ejecutar turnos y las decisiones del bot corresponde al motor de partidas, todavía no implementado.

## API autenticada con el JWT de la plataforma

| Operación | Ruta | Resultado |
| --- | --- | --- |
| Iniciar | `POST /api/player/matchmaking/ranked` con `{ "deckId": "..." }` | Búsqueda del jugador |
| Consultar y renovar presencia | `GET /api/player/matchmaking/current` | Última búsqueda o `204` si no existe una aplicable |
| Salir | `DELETE /api/player/matchmaking/{searchId}` | Cancelada; conserva Expired si ya venció |
| Aceptar bot | `POST /api/player/matchmaking/{searchId}/bot` sin cuerpo | Partida asignada |

La aceptación anticipada, en torneo o con una búsqueda cerrada devuelve `409`. Una búsqueda ajena devuelve `404`. Si una partida humana se asignó primero, aceptar al bot devuelve esa partida sin crear otra. Repetir la aceptación devuelve el mismo identificador.

Cancelar, expirar, emparejar y aceptar al bot comparten el mismo bloqueo transaccional. Una búsqueda cerrada nunca se usa como rival. Si guardar la partida falla, se revierten partida, participante y cambio de estado; la búsqueda permanece abierta.

## Actualizar la base existente

Desde la raíz, con Docker y PostgreSQL iniciados:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/apply-matchmaking-migration.ps1
dotnet run --project backend/src/PokeArena.Api --launch-profile http
```

El script aplica `008` y `009`; no borra cartas, inventarios ni saldos. En bases nuevas Docker Compose las carga al crear el volumen. No es necesario eliminar el volumen existente.

`009` agrega presencia, estado Expired y datos del bot. La columna antigua `bot_available` de `008` se conserva por compatibilidad, pero la API calcula la oferta y no usa ese valor guardado.

## Comprobación funcional

1. Iniciar sesión y buscar con un deck válido. La pantalla consulta cada 2 segundos.
2. Sin otro jugador buscando, mantener abierta la pantalla al menos 60 segundos: aparece «Jugar contra un bot».
3. Ignorar la oferta: continúa `Searching`. Aceptarla: devuelve `Matched` con su identificador y conserva el deck.
4. Con otra búsqueda, salir antes del emparejamiento: queda `Cancelled`.
5. Para comprobar expiración, cerrar todas las pestañas que consultan ese jugador durante más de 60 segundos. Al regresar se informa `Expired`; iniciar otra búsqueda crea una nueva.
6. Si ya se encontró partida, intentar salir desde la API: devuelve `409` y la partida asignada.

Se requiere la vista real `public.matchmaking_deck_source` del módulo de mazos para iniciar una búsqueda; esta implementación no crea decks de sustitución.

## Pruebas automatizadas sin mocks

Con Docker iniciado, desde la raíz:

```powershell
$env:POKEARENA_INTEGRATION_TESTS = '1'
dotnet test backend/tests/PokeArena.Tests/PokeArena.Tests.csproj --filter FullyQualifiedName~Matchmaking
Remove-Item Env:POKEARENA_INTEGRATION_TESTS
```

Usan PostgreSQL temporal para verificar expiración, límites de tiempo, consultas, cancelación, reintentos, carreras con rivales/bot y recuperación ante fallos de persistencia. No alteran la base local del proyecto.
