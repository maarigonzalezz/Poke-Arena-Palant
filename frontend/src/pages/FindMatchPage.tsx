import { useEffect, useRef, useState } from "react";
import { acceptBotMatch, cancelMatchSearch, getCurrentMatchSearch, getMatchmakingDecks, startRankedSearch,
  type MatchmakingDeckOption, type MatchSearch } from "../services/playerApi";
import Icon from "../components/Icon";
import { GAME_RULES } from "../constants/gameRules";
import "./FindMatchPage.css";

const POLL_INTERVAL_MS = 2000;
const REQUIRED_CUBES = 8;

// Solo elige un deck y refleja el estado real de la búsqueda del servidor.
export default function FindMatchPage() {
  const [decks, setDecks] = useState<MatchmakingDeckOption[]>([]);
  const [deckId, setDeckId] = useState("");
  const [search, setSearch] = useState<MatchSearch | null>(null);
  const [error, setError] = useState("");
  const [deckError, setDeckError] = useState("");
  const [statusError, setStatusError] = useState("");
  const [loading, setLoading] = useState(true);
  const [reload, setReload] = useState(0);
  const [busy, setBusy] = useState(false);
  const request = useRef<AbortController | null>(null);
  const revision = useRef(0);
  const locked = search?.status === "Searching" || search?.status === "Matched";

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

  async function act(action: "start" | "cancel" | "bot") {
    if (request.current || busy) return;
    const controller = new AbortController();
    request.current = controller;
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
        setStatusError("");
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

  const selectedDeck = decks.find(deck => deck.id === deckId);
  const searching = search?.status === "Searching";
  const matched = search?.status === "Matched";

  function refresh() {
    setLoading(true); setDeckError(""); setStatusError(""); setReload(value => value + 1);
  }

  return <section className="player-page matchmaking-page" aria-labelledby="matchmaking-title">
    <header className="player-page-heading matchmaking-heading">
      <div><p className="eyebrow">TU PRÓXIMO DESAFÍO</p><h1 id="matchmaking-title">Buscar partida</h1>
        <p>Prepara tu estrategia. La arena te espera.</p></div>
      <span className="matchmaking-mode"><Icon name="swords" /> {search?.context === "Tournament" ? "Torneo" : "Clasificatoria"}</span>
    </header>

    <div className="matchmaking-layout">
      <form className="matchmaking-setup" onSubmit={event => { event.preventDefault(); void act("start"); }} aria-busy={loading || busy}>
        <header className="matchmaking-panel-heading">
          <span className="matchmaking-step">01 <span>/ PREPARACIÓN</span></span>
          <button type="button" className="matchmaking-refresh" disabled={loading || busy} onClick={refresh} aria-label="Actualizar mazos y estado de búsqueda">
            <Icon name="refresh" /><span>Actualizar</span>
          </button>
        </header>
        <h2>Tu mazo, tu estrategia</h2>
        <p className="matchmaking-intro">Elige con cuál de tus mazos quieres entrar a la arena.</p>

        <label className="matchmaking-deck-label">Mazo para esta partida
          <select value={deckId} disabled={loading || busy || locked || !!deckError} onChange={event => { setDeckId(event.target.value); setError(""); }}>
            <option value="">Selecciona un mazo</option>
            {decks.map(deck => <option key={deck.id} value={deck.id}>{deck.name}</option>)}
            {locked && !selectedDeck && <option value={deckId}>Mazo de la búsqueda actual</option>}
          </select>
        </label>

        <div className={`matchmaking-deck-preview${deckId ? " has-selection" : ""}`}>
          <span className="matchmaking-deck-art" aria-hidden="true"><span /><span /><span><Icon name="card" /></span></span>
          <div><span>{locked ? "MAZO RESERVADO" : "TU SELECCIÓN"}</span>
            <strong>{selectedDeck?.name ?? (locked ? "Mazo seleccionado" : "Elige tu próximo movimiento")}</strong>
            <p>{locked ? "Se conservará durante la búsqueda y la partida." : `Tu mazo debe incluir ${GAME_RULES.deckSize} cartas distintas de tu colección.`}</p>
          </div>
          {deckId && <span className="matchmaking-selection-mark"><Icon name="check" /></span>}
        </div>

        {!loading && !deckError && decks.length === 0 && !locked && <p className="matchmaking-empty">Aún no tienes mazos guardados. Crea uno en <strong>Mis mazos</strong> y actualiza la lista para comenzar.</p>}
        {deckError && <p className="alert error" role="alert">{deckError}</p>}

        <div className="matchmaking-entry-note"><span className="matchmaking-cube" aria-hidden="true">◈</span>
          <p>Necesitas al menos <strong>{REQUIRED_CUBES} cubos</strong> disponibles para buscar una partida clasificatoria.</p>
        </div>
        <button className="player-primary matchmaking-start" type="submit" disabled={loading || busy || locked || !!deckError || !deckId}>
          <Icon name={matched ? "check" : "swords"} />
          {busy ? "Procesando…" : loading ? "Consultando tus mazos…" : searching ? "Búsqueda en curso" : matched ? "Partida asignada" : "Buscar partida"}
          {!busy && !loading && !locked && <Icon name="arrow" />}
        </button>
        <p className="matchmaking-setup-hint"><Icon name="shield" />{matched ? "El mazo se mantiene fijo durante la partida." : searching ? "Para usar otro mazo, debes salir de la búsqueda." : "Jugaremos con el mazo que elijas aquí."}</p>
      </form>

      <div className={`matchmaking-arena${searching ? " is-searching" : ""}${matched ? " is-matched" : ""}`}>
        <MatchmakingStatus search={search} loading={loading} />
        {searching && <div className="matchmaking-search-actions">
          {search.botAvailable && <div className="matchmaking-bot-offer">
            <span className="matchmaking-bot-icon"><Icon name="bot" /></span>
            <div><strong>El bot también está listo</strong><p>Juega ahora o sigue esperando un rival. Tú decides.</p></div>
            <button type="button" className="player-primary" disabled={busy} onClick={() => void act("bot")}><Icon name="bot" />Jugar contra el bot</button>
          </div>}
          <button type="button" className="matchmaking-cancel" disabled={busy} onClick={() => void act("cancel")}>Salir de la búsqueda</button>
        </div>}
      </div>
    </div>

    {statusError && <div className="matchmaking-error"><p className="alert error" role="alert">{statusError}</p><button type="button" className="matchmaking-refresh" disabled={loading || busy} onClick={refresh}><Icon name="refresh" />Volver a consultar</button></div>}
    {error && <p className="alert error" role="alert">{error}</p>}

    <footer className="matchmaking-guide" aria-label="Cómo funciona la búsqueda">
      <div><span><Icon name="card" /></span><div><strong>Un mazo propio</strong><p>{GAME_RULES.deckSize} cartas distintas de tu colección.</p></div></div>
      <div><span><Icon name="shield" /></span><div><strong>Un rival de tu nivel</strong><p>El rango Elo se amplía mientras esperas.</p></div></div>
      <div><span><Icon name="clock" /></span><div><strong>Tú eliges cuándo salir</strong><p>Puedes cancelar antes de encontrar partida.</p></div></div>
    </footer>
  </section>;
}

// Presenta el estado confirmado por la API sin inventar tiempos ni resultados.
function MatchmakingStatus({ search, loading }: { search: MatchSearch | null; loading: boolean }) {
  const searching = search?.status === "Searching";
  const matched = search?.status === "Matched";
  const expired = search?.status === "Expired";
  const cancelled = search?.status === "Cancelled";
  const title = loading ? "Preparando la arena" : searching ? "Buscando a tu rival" : matched ? "¡Partida encontrada!"
    : expired ? "La búsqueda expiró" : cancelled ? "Búsqueda cancelada" : "Tu próximo rival te espera";
  const description = loading ? "Estamos consultando tus mazos y el estado de tu búsqueda."
    : searching ? "Buscamos un jugador con un Elo cercano al tuyo. Mantén esta pantalla abierta."
      : matched ? "Tu mazo quedó registrado. Ya tienes una partida asignada."
        : expired ? search.message ?? "La búsqueda terminó por inactividad. Puedes iniciar una nueva."
          : cancelled ? search.message ?? "Puedes elegir otro mazo y volver cuando estés listo."
            : "Elige tu mazo y encuentra un nuevo desafío. Nosotros buscamos al oponente.";

  return <div className="matchmaking-status" role="status" aria-live="polite" aria-atomic="true">
    <span className={`matchmaking-status-tag${searching ? " is-active" : ""}`}><span />
      {loading ? "CONECTANDO" : searching ? "EN BÚSQUEDA" : matched ? "RIVAL ASIGNADO" : expired || cancelled ? "BÚSQUEDA FINALIZADA" : "LA ARENA TE ESPERA"}
    </span>
    <div className="matchmaking-emblem" aria-hidden="true"><span /><span /><div><Icon name={matched ? "check" : expired ? "clock" : searching || loading ? "search" : "swords"} /></div></div>
    <h2>{title}</h2><p className="matchmaking-status-description">{description}</p>
    {searching && <>
      <dl className="matchmaking-elo"><div><dt>Tu Elo</dt><dd>{search.elo}</dd></div><div><dt>Rango actual</dt><dd>±{search.acceptedEloDifference}<small> Elo</small></dd></div></dl>
      <p className="matchmaking-range-note">Comienza en ±100 y aumenta 50 cada 15 segundos, hasta ±500.</p>
      {search.context === "Tournament" && <p className="matchmaking-range-note">Búsqueda de torneo en curso.</p>}
    </>}
    {matched && <div className="matchmaking-match-id"><span>IDENTIFICADOR DE PARTIDA</span><code>{search.matchId}</code></div>}
  </div>;
}
