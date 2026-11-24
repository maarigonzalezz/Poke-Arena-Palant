import { useCallback, useEffect, useRef, useState } from "react";
import { acceptBotMatch, advanceMatchTurn, cancelMatchSearch, getCurrentMatchSearch, getMatchmakingDecks,
  getMatchState, playMatchCard, discardMatchCard, startRankedSearch,
  type MatchBoardCardState, type MatchCardState, type MatchmakingDeckOption, type MatchSearch, type MatchState } from "../services/playerApi";
import { API_BASE_URL } from "../services/api";

const POLL_INTERVAL_MS = 2000;

// Normaliza rutas relativas de cartas y ubicaciones usando el origen de la API.
function matchImage(path: string | null | undefined) {
  return path ? new URL(path, `${API_BASE_URL}/`).href : null;
}

/**
 * Pantalla de matchmaking y partida activa. Las acciones se envían a la API y el estado autoritativo
 * se vuelve a leer del servidor; esta página no calcula reglas ni modifica el tablero de forma optimista.
 */
export default function FindMatchPage({ onBuildDeck }: { onBuildDeck: () => void }) {
  const [decks, setDecks] = useState<MatchmakingDeckOption[]>([]);
  const [deckId, setDeckId] = useState("");
  const [search, setSearch] = useState<MatchSearch | null>(null);
  const [error, setError] = useState("");
  const [deckError, setDeckError] = useState("");
  const [statusError, setStatusError] = useState("");
  const [loading, setLoading] = useState(true);
  const [reload, setReload] = useState(0);
  const [busy, setBusy] = useState(false);
  const [matchState, setMatchState] = useState<MatchState | null>(null);
  const [battleEvents, setBattleEvents] = useState<string[]>([]);
  const [secondsLeft, setSecondsLeft] = useState(30);
  const request = useRef<AbortController | null>(null);
  const revision = useRef(0);
  const automaticAdvanceTurn = useRef<number | null>(null);
  const previousMatchState = useRef<MatchState | null>(null);
  const locked = search?.status === "Searching"
    || (search?.status === "Matched" && matchState?.status !== "Completed");
  const activeMatchId = matchState?.status === "Active" ? matchState.matchId : null;
  const currentTurnEndsAt = matchState?.turnEndsAt;

  /** Compara snapshots consecutivos para registrar eventos visibles sin mostrar información privada rival. */
  const receiveMatchState = useCallback((next: MatchState) => {
    // Los eventos se derivan de dos snapshots del servidor, no de acciones locales optimistas.
    const previous = previousMatchState.current;
    const events: string[] = [];
    if (!previous || previous.matchId !== next.matchId) {
      events.push(`Partida iniciada. Ambos campos están listos para el turno ${next.turnNumber}.`);
    } else {
      if (next.turnNumber > previous.turnNumber) {
        events.push(`Comienza el turno ${next.turnNumber}: tú tienes ${next.player.energy} de energía y el rival ${next.opponent.energy}.`);
      }
      const locationName = (slot: number) => next.locations.find(location => location.slot === slot)?.name ?? `ubicación ${slot}`;
      const previousOwnBoard = new Set(previous.player.boardCards.map(card => card.id));
      for (const card of next.player.boardCards.filter(card => !previousOwnBoard.has(card.id))) {
        events.push(`Jugaste ${card.name} en ${locationName(card.locationSlot)}.`);
      }
      const previousOpponentBoard = new Set(previous.opponent.boardCards.map(card => card.id));
      for (const card of next.opponent.boardCards.filter(card => !previousOpponentBoard.has(card.id))) {
        events.push(`El rival jugó ${card.name} en ${locationName(card.locationSlot)}.`);
      }
      const previousOwnCemetery = new Set(previous.player.cemeteryCards.map(card => card.id));
      for (const card of next.player.cemeteryCards.filter(card => !previousOwnCemetery.has(card.id))) {
        events.push(`Enviaste ${card.name} a tu cementerio.`);
      }
      const previousOpponentCemetery = new Set(previous.opponent.cemeteryCards.map(card => card.id));
      for (const card of next.opponent.cemeteryCards.filter(card => !previousOpponentCemetery.has(card.id))) {
        events.push(`El rival envió ${card.name} a su cementerio.`);
      }
      if (previous.status === "Active" && next.status === "Completed") events.push("La partida finalizó.");
    }
    if (events.length) setBattleEvents(current => [...events, ...current].slice(0, 5));
    previousMatchState.current = next;
    setMatchState(next);
  }, []);

  /** Solicita el avance cuando vence el reloj y aplica el estado actualizado que devuelve el servidor. */
  const advanceTurn = useCallback(async () => {
    if (!matchState || matchState.status !== "Active" || busy || secondsLeft > 0) return;
    setBusy(true); setError("");
    try {
      const state = await advanceMatchTurn(matchState.matchId, matchState.turnNumber);
      receiveMatchState(state);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "No se pudo avanzar el turno.");
    } finally {
      setBusy(false);
    }
  }, [matchState, busy, secondsLeft, receiveMatchState]);

  /** Ejecuta una acción de carta y conserva el snapshot anterior si la petición falla. */
  async function actWithCard(action: "play" | "discard", cardId: number, locationSlot?: number) {
    if (!matchState || busy || secondsLeft <= 0) return;
    setBusy(true); setError("");
    try {
      const state = action === "play"
        ? await playMatchCard(matchState.matchId, cardId, locationSlot!)
        : await discardMatchCard(matchState.matchId, cardId);
      receiveMatchState(state);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "No se pudo completar la acción con la carta.");
    } finally {
      setBusy(false);
    }
  }

  // Carga de forma independiente los mazos y la búsqueda existente para que un fallo no oculte el otro.
  useEffect(() => {
    const controller = new AbortController();
    // Una integración pendiente no impide recuperar una búsqueda que ya existe.
    void Promise.allSettled([
      getMatchmakingDecks(controller.signal).then(data => {
        if (!controller.signal.aborted) setDecks(data);
      }).catch((cause: unknown) => {
        if (!controller.signal.aborted) setDeckError(cause instanceof Error ? cause.message : "No se pudieron consultar sus decks.");
      }),
      getCurrentMatchSearch(controller.signal).then(current => {
        if (!controller.signal.aborted) {
          setSearch(current);
          if (current) setDeckId(current.deckId);
        }
      }).catch((cause: unknown) => {
        if (!controller.signal.aborted) setStatusError(cause instanceof Error ? cause.message : "No se pudo consultar la búsqueda.");
      }),
    ]).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => { controller.abort(); request.current?.abort(); };
  }, [reload]);

  // Sondeo de cola para reflejar cambios iniciados en otra pestaña o por el emparejador del servidor.
  useEffect(() => {
    // También detecta búsquedas de otra pestaña y partidas que ya finalizaron.
    if (loading) return;
    const controller = new AbortController();
    let timer: number;
    async function poll() {
      const version = revision.current;
      try {
        if (request.current) return;
        const current = await getCurrentMatchSearch(controller.signal);
        if (!controller.signal.aborted && version === revision.current) {
          setSearch(current);
          if (current?.status === "Searching" || current?.status === "Matched") setDeckId(current.deckId);
          setStatusError("");
        }
      } catch (cause) {
        if (!controller.signal.aborted && version === revision.current)
          setStatusError(cause instanceof Error ? cause.message : "No se pudo actualizar la búsqueda.");
      } finally {
        if (!controller.signal.aborted) timer = window.setTimeout(() => { void poll(); }, POLL_INTERVAL_MS);
      }
    }
    timer = window.setTimeout(() => { void poll(); }, POLL_INTERVAL_MS);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [loading]);

  // Al emparejarse, recupera el primer snapshot completo de la partida.
  useEffect(() => {
    if (search?.status !== "Matched" || !search.matchId) return;
    const controller = new AbortController();
    void getMatchState(search.matchId, controller.signal)
      .then(state => { if (!controller.signal.aborted) receiveMatchState(state); })
      .catch(cause => {
        if (!controller.signal.aborted)
          setError(cause instanceof Error ? cause.message : "No se pudo consultar la partida.");
      });
    return () => controller.abort();
  }, [search?.status, search?.matchId, receiveMatchState]);

  // Actualiza el tablero durante la partida; los cambios siempre provienen de snapshots de la API.
  useEffect(() => {
    if (!activeMatchId) return;
    const controller = new AbortController();
    const timer = window.setInterval(() => {
      if (busy) return;
      void getMatchState(activeMatchId, controller.signal)
        .then(state => { if (!controller.signal.aborted) receiveMatchState(state); })
        .catch(cause => {
          if (!controller.signal.aborted)
            setError(cause instanceof Error ? cause.message : "No se pudo actualizar la partida.");
        });
    }, POLL_INTERVAL_MS);
    return () => { controller.abort(); window.clearInterval(timer); };
  }, [activeMatchId, busy, receiveMatchState]);

  // Renderiza el reloj con la fecha límite del servidor en vez de contar segundos localmente desde cero.
  useEffect(() => {
    if (!currentTurnEndsAt) return;
    // El reloj usa la fecha límite del servidor para no acumular deriva entre renders.
    const update = () => setSecondsLeft(Math.max(0,
      Math.ceil((new Date(currentTurnEndsAt).getTime() - Date.now()) / 1000)));
    update();
    const timer = window.setInterval(update, 250);
    return () => window.clearInterval(timer);
  }, [currentTurnEndsAt]);

  // El servidor es quien valida y resuelve el avance automático una sola vez por turno.
  useEffect(() => {
    if (!matchState || matchState.status !== "Active" || secondsLeft > 0
      || automaticAdvanceTurn.current === matchState.turnNumber) return;
    // Evita enviar más de un avance automático para el mismo turno.
    automaticAdvanceTurn.current = matchState.turnNumber;
    void advanceTurn();
  }, [advanceTurn, matchState, secondsLeft]);

  /** Inicia, cancela o acepta una búsqueda de bot; recupera el estado actual si hay una carrera con otra pestaña. */
  async function act(action: "start" | "cancel" | "bot") {
    if (request.current || busy) return;
    const controller = new AbortController();
    request.current = controller;
    // La revisión descarta polls que terminen después de una acción del jugador.
    revision.current++;
    setBusy(true); setError("");
    try {
      const result = action === "bot" && search
        ? await acceptBotMatch(search.id, controller.signal)
        : action === "cancel" && search
          ? await cancelMatchSearch(search.id, controller.signal)
          : await startRankedSearch(deckId, controller.signal);
      if (!controller.signal.aborted) {
        setSearch(result);
        if (action === "start" && result.status === "Searching") {
          previousMatchState.current = null;
          setBattleEvents([]);
          setMatchState(null);
        }
        setStatusError("");
        if (result.status === "Matched" && result.matchId) {
          try {
            receiveMatchState(await getMatchState(result.matchId, controller.signal));
          } catch {
            setError("La partida fue creada, pero no se pudo consultar su estado.");
          }
        }
      }
    } catch (cause) {
      if (!controller.signal.aborted) {
        setError(cause instanceof Error ? cause.message : "No se pudo completar la operación.");
        // Otra pestaña puede haber iniciado o emparejado la búsqueda mientras se enviaba.
        try {
          const current = await getCurrentMatchSearch(controller.signal);
          if (!controller.signal.aborted) { setSearch(current); if (current) setDeckId(current.deckId); }
        } catch { /* Conserva el error original de la operación. */ }
      }
    } finally {
      // Invalida consultas anteriores para que no restauren una búsqueda ya cancelada.
      revision.current++;
      if (request.current === controller) request.current = null;
      if (!controller.signal.aborted) setBusy(false);
    }
  }

  const isPlaying = search?.status === "Matched" && matchState !== null;

  return <section className={`player-page match-page${isPlaying ? " match-page-in-game" : ""}`}>
    <header className="match-page-heading">
      <div>
        <p className="eyebrow">LA ARENA TE ESPERA</p>
        <h1>{isPlaying ? "Campo de batalla" : "Encuentra tu rival"}</h1>
        <p>{isPlaying ? "Lee el campo, administra tu energía y juega tu mejor estrategia." : "Elige tu mazo y demuestra de qué está hecha tu estrategia."}</p>
      </div>
      <div className="match-season-mark"><span>✦</span><div><strong>CLASIFICATORIA</strong><small>Temporada de la arena</small></div></div>
    </header>

    {(!search || search.status === "Cancelled" || search.status === "Expired") && <form className="queue-card"
      onSubmit={event => { event.preventDefault(); void act("start"); }}>
      <div className="queue-card-emblem" aria-hidden="true">⚔</div>
      <div className="queue-card-copy">
        <span className="match-kicker">MODO CLASIFICATORIO</span>
        <h2>Una nueva batalla comienza aquí</h2>
        <p>Selecciona un mazo de 12 cartas. La arena y el orden del mazo se preparan al azar para cada partida.</p>
      </div>
      <div className="queue-card-controls">
        <label htmlFor="match-deck">Tu mazo</label>
        <select id="match-deck" value={deckId} disabled={loading || busy || locked || !!deckError}
          onChange={event => { setDeckId(event.target.value); setError(""); }}>
          <option value="">Selecciona un mazo</option>
          {decks.map(deck => <option key={deck.id} value={deck.id}>{deck.name}</option>)}
          {locked && !decks.some(deck => deck.id === deckId) && <option value={deckId}>{deckId}</option>}
        </select>
        <button className="match-primary-button" type="submit"
          disabled={loading || busy || locked || !!deckError || !deckId}>
          <span aria-hidden="true">⚔</span>{busy ? "Preparando arena…" : "Buscar partida"}
        </button>
      </div>
      <div className="queue-card-footer"><span>◈ Apuesta de 4 cubos</span><span>◷ Turnos de 30 segundos</span><span>✦ 3 ubicaciones por partida</span></div>
    </form>}

    {!loading && decks.length === 0 && !deckError && !search?.matchId && <div className="match-empty-state">
      <span className="match-empty-icon">▧</span>
      <div><h2>Tu primera estrategia empieza con un mazo</h2><p>Construye un mazo de 12 cartas para entrar a la arena.</p></div>
      <button type="button" className="match-secondary-button" onClick={onBuildDeck}>Ir a mis mazos <span>→</span></button>
    </div>}

    {loading && <div className="match-loading" role="status"><span className="match-loader" /> Preparando tu mesa…</div>}
    {search?.status === "Searching" && <div className="queue-search-card" role="status">
      <div className="search-pulse" aria-hidden="true"><span>⚔</span></div>
      <div className="queue-search-info">
        <span className="match-kicker">BUSCANDO RIVAL · {search.context === "Tournament" ? "TORNEO" : "CLASIFICATORIA"}</span>
        <h2>Tu mazo está en la mesa</h2>
        <p>Estamos buscando un rival con una puntuación cercana a tu Elo de <strong>{search.elo}</strong>.</p>
        <div className="elo-track"><span style={{ width: `${Math.min(search.acceptedEloDifference / 500 * 100, 100)}%` }} /></div>
        <small>Rango de emparejamiento: ±{search.acceptedEloDifference} · Se amplía mientras esperas</small>
      </div>
      <div className="queue-search-actions">
        {search.botAvailable && <button type="button" className="match-primary-button" disabled={busy} onClick={() => void act("bot")}>Jugar contra un bot</button>}
        <button type="button" className="match-secondary-button" disabled={busy} onClick={() => void act("cancel")}>Cancelar búsqueda</button>
      </div>
      {search.message && <p className="match-inline-message" role="alert">{search.message}</p>}
    </div>}

    {search?.status === "Matched" && <div className="battle-console">
      {matchState ? matchState.status === "Completed" ? <CompletedMatchResult
        state={matchState} onPlayAgain={() => void act("start")} onBuildDeck={onBuildDeck} busy={busy} /> : <>
        <div className="battle-statusbar">
          <div className="battle-opponent">
            <span className="battle-avatar">{matchState.isBot ? "✦" : "⚔"}</span>
            <span><small>RIVAL</small><strong>{matchState.isBot ? "Guardián de la arena" : "Entrenador rival"}</strong></span>
            <i aria-label="Rival conectado" />
            <div className="battle-opponent-stats" aria-label="Información pública del rival">
              <span>⚡ {matchState.opponent.energy} energía</span>
              <span>▤ {matchState.opponent.handCardCount} en mano</span>
              <span>▧ {matchState.opponent.remainingDeckCards} en mazo</span>
              <span>† {matchState.opponent.cemeteryCards.length} en cementerio</span>
            </div>
          </div>
          <div className="battle-turn">
            <span className="battle-turn-label">{matchState.status === "Active" ? "TURNO" : "PARTIDA FINALIZADA"}</span>
            <strong>{matchState.status === "Active" ? String(matchState.turnNumber).padStart(2, "0") : "✦"}</strong>
            <small>de 6</small>
          </div>
          <div className={`battle-clock${secondsLeft <= 8 && matchState.status === "Active" ? " is-urgent" : ""}`}>
            <span aria-hidden="true">◷</span>
            <div><small>TIEMPO</small><strong>{matchState.status === "Active" ? `00:${String(secondsLeft).padStart(2, "0")}` : "FINAL"}</strong></div>
          </div>
          <div className="battle-wager"><span>◈</span><div><small>APUESTA</small><strong>{matchState.wagerCubes} cubos</strong></div></div>
        </div>

        <section className="battle-log" aria-label="Actividad reciente de la partida" aria-live="polite">
          <strong>ACTIVIDAD DE LA PARTIDA</strong>
          <ol>{battleEvents.map((event, index) => <li key={`${event}-${index}`}>{event}</li>)}</ol>
        </section>

        <div className="battle-arena">
          <div className="arena-caption"><span>CAMPO DE BATALLA</span><i /> <small>{matchState.context === "Tournament" ? "TORNEO" : "CLASIFICATORIA"}</small>
            <span className="arena-legend"><b className="legend-rival" /> Campo rival <b className="legend-player" /> Tu campo</span>
          </div>
          <div className="arena-location-grid">
            {matchState.locations.map(location => {
              const cards = matchState.player.boardCards.filter(card => card.locationSlot === location.slot);
              const opponentCards = matchState.opponent.boardCards.filter(card => card.locationSlot === location.slot);
              const restricted = location.restrictedTurns.includes(matchState.turnNumber);
              const isFull = cards.length >= location.maxCards;
              const opponentIsFull = opponentCards.length >= location.maxCards;
              const imageUrl = matchImage(location.imageSmallPath);
              return <article className={`arena-location${restricted ? " is-restricted" : ""}`} key={location.id}>
                <div className="location-art" style={imageUrl ? { backgroundImage: `linear-gradient(180deg, #12151a08 15%, #12151acc 100%), url("${imageUrl}")` } : undefined}>
                  <span className="location-slot">UBICACIÓN 0{location.slot}</span>
                  {restricted && <span className="location-lock">RESTRINGIDA · TURNO {matchState.turnNumber}</span>}
                  {!imageUrl && <span className="location-fallback-mark">✦</span>}
                  <div className="location-title"><h3>{location.name}</h3><p>{location.description || "Un nuevo escenario para la batalla."}</p></div>
                </div>
                <div className="location-rules">
                  <span>Espacios por jugador: {location.maxCards}</span>
                  <span>⚡ {location.energyMode === "Fixed" ? `+${location.extraEnergy} energía` : `+${location.extraEnergy} por carta`}</span>
                </div>
                <div className="location-board">
                  <div className="location-side-row opponent-side">
                    <div className="location-side-label"><span>RIVAL</span><small>{opponentCards.length}/{location.maxCards}</small></div>
                    <div className="location-side-cards">
                      {opponentCards.map(card => <BoardCard key={card.id} card={card} />)}
                      {!opponentCards.length && <span className="location-empty-slot">Sin cartas del rival</span>}
                      {opponentIsFull && <span className="location-full-badge">LLENO</span>}
                    </div>
                  </div>
                  <div className="location-side-row player-side">
                    <div className="location-side-label"><span>TÚ</span><small>{cards.length}/{location.maxCards}</small></div>
                    <div className="location-side-cards">
                      {cards.map(card => <BoardCard key={card.id} card={card} />)}
                      {!cards.length && <span className="location-empty-slot">Tu espacio para jugar</span>}
                      {isFull && <span className="location-full-badge">LLENO</span>}
                    </div>
                  </div>
                </div>
              </article>;
            })}
          </div>
          <div className="arena-player-bar">
            <span className="arena-player-emblem">◈</span>
            <span><small>TU CAMPO</small><strong>{matchState.player.boardCards.length} cartas en juego</strong></span>
            <span className="arena-own-energy">⚡ {matchState.player.energy} energía disponible</span>
            <span className="arena-deck-count">▤ {matchState.player.remainingDeckCards} en el mazo</span>
            <span className="arena-cemetery-count">† {matchState.player.cemeteryCards.length} en tu cementerio</span>
          </div>
        </div>

        <div className="battle-hand-panel">
          <div className="battle-hand-heading">
            <div><span className="match-kicker">TU ESTRATEGIA</span><h2>Mano de cartas <span>{matchState.player.handCards.length}</span></h2></div>
            <div className="energy-orb"><span>⚡</span><div><strong>{matchState.player.energy}</strong><small>ENERGÍA</small></div></div>
          </div>
          <p className="battle-action-hint">Elige una ubicación disponible para jugar una carta. Si la descartas, irá a tu cementerio.</p>
          {matchState.player.handCards.length > 0 ? <div className="battle-hand">
            {matchState.player.handCards.map(card => {
              const imageUrl = matchImage(card.imagePath ?? card.imageSmallPath);
              return <article className="battle-card" key={card.id}>
                <div className="battle-card-meta">
                  <span className="battle-card-cost">⚡ {card.cost}</span>
                  <span className="battle-card-type">{card.cardType}</span>
                </div>
                <div className="battle-card-art">
                  {imageUrl ? <img src={imageUrl} alt={`Ilustración completa de ${card.name}`} loading="lazy" /> : <span className="battle-card-fallback">✦</span>}
                </div>
                <div className="battle-card-info"><strong>{card.name}</strong><span>✦ {card.power} poder</span></div>
                <div className="battle-card-actions">
                  {matchState.locations.map(location => {
                    const count = matchState.player.boardCards.filter(boardCard => boardCard.locationSlot === location.slot).length;
                    const restricted = location.restrictedTurns.includes(matchState.turnNumber);
                    const unavailableReason = busy ? "Procesando otra acción"
                      : secondsLeft <= 0 ? "El tiempo del turno terminó"
                        : matchState.status !== "Active" ? "La partida ya terminó"
                          : location.maxCards === 0 ? "Esta ubicación no admite cartas"
                            : count >= location.maxCards ? "Tu lado de esta ubicación está lleno"
                              : restricted ? `No se puede jugar aquí en el turno ${matchState.turnNumber}`
                                : card.cost > matchState.player.energy ? "No tienes energía suficiente"
                                  : `Jugar en ${location.name}`;
                    const unavailable = busy || secondsLeft <= 0 || matchState.status !== "Active"
                      || location.maxCards === 0 || count >= location.maxCards || restricted
                      || card.cost > matchState.player.energy;
                    return <button key={location.id} type="button"
                      aria-label={`${unavailable ? unavailableReason : `Jugar ${card.name} en ${location.name}`}`}
                      title={unavailableReason} disabled={unavailable}
                      onClick={() => void actWithCard("play", card.id, location.slot)}>
                      <span>{unavailable ? "⊘" : "＋"}</span>{location.name}
                    </button>;
                  })}
                  <button className="discard-card-button" type="button" disabled={busy || secondsLeft <= 0 || matchState.status !== "Active"}
                    onClick={() => void actWithCard("discard", card.id)}>Descartar</button>
                </div>
              </article>;
            })}
          </div> : <div className="empty-hand"><span>▤</span><strong>Tu mano está vacía</strong><small>Roba una carta al inicio del próximo turno.</small></div>}
          <div className="cemetery-trays">
            <CemeteryTray title="Tu cementerio" cards={matchState.player.cemeteryCards} />
            <CemeteryTray title="Cementerio rival" cards={matchState.opponent.cemeteryCards} />
          </div>
          <div className="battle-hand-footer">
            <span>Se actualiza automáticamente al jugarse una acción o avanzar el turno.</span>
            <button type="button" className="match-secondary-button" disabled={busy || secondsLeft > 0 || matchState.status !== "Active"}
              onClick={() => void advanceTurn()}>{busy ? "Resolviendo…" : secondsLeft > 0 ? `Siguiente turno en ${secondsLeft}s` : "Continuar al siguiente turno"} <span>→</span></button>
          </div>
        </div>
      </> : <div className="match-loading" role="status"><span className="match-loader" /> Preparando las ubicaciones y barajando los mazos…</div>}
    </div>}

    {search?.status === "Cancelled" && <p className="match-inline-message" role="status">{search.message ?? "Saliste de la búsqueda. Puedes elegir otro mazo."}</p>}
    {search?.status === "Expired" && <p className="match-inline-message" role="status">{search.message}</p>}
    {statusError && <p className="alert error" role="alert">{statusError}</p>}
    {error && <p className="alert error" role="alert">{error}</p>}
    {deckError && <p className="alert error" role="alert">{deckError}</p>}
    {(deckError || statusError) && <button type="button" className="match-secondary-button" disabled={loading || busy} onClick={() => {
      setLoading(true); setDeckError(""); setStatusError(""); setReload(value => value + 1);
    }}>Volver a consultar</button>}
  </section>;
}

/** Presenta una carta jugada con su imagen completa y estadísticas en el campo. */
function BoardCard({ card }: { card: MatchBoardCardState }) {
  const imageUrl = matchImage(card.imagePath);
  return <article className="board-card" title={`${card.name} · Costo ${card.cost} · Poder ${card.power}`}>
    <div className="board-card-art">{imageUrl
      ? <img src={imageUrl} alt={`Ilustración completa de ${card.name}`} loading="lazy" />
      : <span aria-hidden="true">✦</span>}</div>
    <strong>{card.name}</strong>
    <span className="board-card-power">⚡ {card.cost} · ✦ {card.power}</span>
  </article>;
}

/** Muestra las cartas descartadas o destruidas sin recortar sus ilustraciones. */
function CemeteryTray({ title, cards }: { title: string; cards: MatchCardState[] }) {
  return <section className="cemetery-tray" aria-label={title}>
    <div className="cemetery-heading"><span className="cemetery-icon">†</span><div><strong>{title}</strong>
      <small>{cards.length} {cards.length === 1 ? "carta" : "cartas"}</small></div></div>
    {cards.length ? <div className="cemetery-cards">
      {cards.map(card => {
        const imageUrl = matchImage(card.imagePath ?? card.imageSmallPath);
        return <article className="cemetery-card" key={card.id}>
          <div className="cemetery-card-art">{imageUrl
            ? <img src={imageUrl} alt={`Ilustración completa de ${card.name} en ${title.toLowerCase()}`} loading="lazy" />
            : <span aria-hidden="true">✦</span>}</div>
          <strong>{card.name}</strong>
          <small>⚡ {card.cost} · ✦ {card.power}</small>
        </article>;
      })}
    </div> : <p className="cemetery-empty">Sin cartas todavía.</p>}
  </section>;
}

/** Resume el ganador por ubicaciones y desempates, el pozo clasificatorio y las acciones posteriores. */
function CompletedMatchResult({ state, onPlayAgain, onBuildDeck, busy }: {
  state: MatchState;
  onPlayAgain: () => void;
  onBuildDeck: () => void;
  busy: boolean;
}) {
  const result = state.result;
  const title = result?.outcome === "Win" ? "¡Victoria!"
    : result?.outcome === "Loss" ? "Partida finalizada"
      : result?.outcome === "Draw" ? "Empate"
        : "Partida finalizada";
  const subtitle = result?.outcome === "Win" ? "Dominaste la arena. ¡Bien jugado!"
    : result?.outcome === "Loss" ? "El rival se lleva esta partida. ¡La siguiente puede ser tuya!"
      : result?.outcome === "Draw" ? "Ningún entrenador logró superar los desempates."
        : "El resultado de esta partida no está disponible.";
  return <section className={`match-result${result?.outcome === "Win" ? " is-victory" : ""}${result?.outcome === "Draw" ? " is-draw" : ""}`}>
    <div className="match-result-hero">
      <span className="match-result-emblem" aria-hidden="true">{result?.outcome === "Win" ? "✦" : result?.outcome === "Loss" ? "⚔" : "◈"}</span>
      <p className="match-kicker">FIN DE LA PARTIDA · TURNO {state.turnNumber}</p>
      <h2>{title}</h2>
      <p>{subtitle}</p>
      {result?.outcome === "Completed" && <p className="match-result-note">Esta partida terminó antes de que se registrara un resultado.</p>}
    </div>
    {result && result.outcome !== "Completed" && <>
      <div className="match-result-score">
        <div><small>TU LADO</small><strong>{result.playerLocationsWon}</strong><span>ubicaciones ganadas</span></div>
        <div className="match-result-score-divider"><span>VS</span></div>
        <div><small>RIVAL</small><strong>{result.opponentLocationsWon}</strong><span>ubicaciones ganadas</span></div>
      </div>
      <div className="match-result-locations">
        {state.locations.map(location => {
          const ownPower = state.player.boardCards.filter(card => card.locationSlot === location.slot)
            .reduce((sum, card) => sum + card.power, 0);
          const rivalPower = state.opponent.boardCards.filter(card => card.locationSlot === location.slot)
            .reduce((sum, card) => sum + card.power, 0);
          const locationOutcome = ownPower > rivalPower ? "Tu victoria"
            : rivalPower > ownPower ? "Victoria rival" : "Empate";
          return <div className="match-result-location" key={location.id}>
            <span>{location.name}</span><strong>{ownPower} <small>—</small> {rivalPower}</strong>
            <em className={ownPower > rivalPower ? "won" : rivalPower > ownPower ? "lost" : ""}>{locationOutcome}</em>
          </div>;
        })}
      </div>
      <div className="match-result-summary">
        <div><small>PODER TOTAL</small><strong>{result.playerTotalPower} <span>—</span> {result.opponentTotalPower}</strong></div>
        <div><small>ENERGÍA RESTANTE</small><strong>{result.playerFinalEnergy} <span>—</span> {result.opponentFinalEnergy}</strong></div>
        <div><small>CEMENTERIO</small><strong>{result.playerCemeteryCount} <span>—</span> {result.opponentCemeteryCount}</strong></div>
      </div>
      <p className="match-result-rule">Se compara el poder en cada ubicación. Si ganan la misma cantidad, desempata la energía restante y luego el menor cementerio.</p>
      {state.context === "Ranked" && <div className="match-result-payout">
        <span>◈</span>
        {result.outcome === "Win" ? `Recibiste ${result.cubesAwarded} cubos del pozo.`
          : result.outcome === "Draw" ? `Se devolvieron ${result.cubesAwarded} cubos de apuesta.`
            : `La apuesta de ${state.wagerCubes} cubos no se recupera.`}
      </div>}
    </>}
    <div className="match-result-actions">
      <button type="button" className="match-primary-button" disabled={busy} onClick={onPlayAgain}>
        {busy ? "Buscando rival…" : "Jugar otra clasificatoria"} <span>→</span>
      </button>
      <button type="button" className="match-secondary-button" onClick={onBuildDeck}>Cambiar mi mazo</button>
    </div>
  </section>;
}
