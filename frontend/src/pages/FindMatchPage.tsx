import { useEffect, useRef, useState } from "react";
import { cancelMatchSearch, getCurrentMatchSearch, getMatchmakingDecks, startRankedSearch,
  type MatchmakingDeckOption, type MatchSearch } from "../services/playerApi";

const POLL_INTERVAL_MS = 2000;

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

  async function act(cancel: boolean) {
    if (request.current || busy) return;
    const controller = new AbortController();
    request.current = controller;
    revision.current++;
    setBusy(true); setError("");
    try {
      const result = cancel && search
        ? await cancelMatchSearch(search.id, controller.signal)
        : await startRankedSearch(deckId, controller.signal);
      if (!controller.signal.aborted) { setSearch(result); setStatusError(""); }
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

  return <section className="player-page">
    <h1>Buscar partida clasificatoria</h1>
    <p>Seleccione uno de sus decks. El servidor comprobará sus cartas y que tenga al menos 8 cubos.</p>
    <form className="panel" onSubmit={event => { event.preventDefault(); void act(false); }}>
      <label>Deck
        <select value={deckId} disabled={loading || busy || locked || !!deckError} onChange={event => { setDeckId(event.target.value); setError(""); }}>
          <option value="">Seleccione un deck</option>
          {decks.map(deck => <option key={deck.id} value={deck.id}>{deck.name}</option>)}
          {locked && !decks.some(deck => deck.id === deckId) && <option value={deckId}>{deckId}</option>}
        </select>
      </label>
      {!loading && !deckError && decks.length === 0 && <p>No tiene decks disponibles. Créelos desde el módulo de mazos.</p>}
      {deckError && <p role="alert">{deckError}</p>}
      <button type="submit" disabled={loading || busy || locked || !!deckError || !deckId}>
        {busy ? "Procesando…" : "Buscar partida"}
      </button>
    </form>
    {loading && <p role="status">Consultando sus decks y búsqueda…</p>}
    {search?.status === "Searching" && <div className="panel" role="status">
      <p>Búsqueda vigente: {search.context === "Tournament" ? "torneo" : "clasificatoria"}. Elo: {search.elo}. Diferencia aceptada actualmente: ±{search.acceptedEloDifference}.</p>
      <p>El rango comienza en ±100 y aumenta 50 cada 15 segundos, hasta ±500.</p>
      <button type="button" disabled={busy} onClick={() => void act(true)}>Salir de la búsqueda</button>
    </div>}
    {search?.status === "Matched" && <div className="panel" role="status">
      <p>Partida encontrada: {search.matchId}</p><p>El deck elegido quedó registrado para esta partida.</p>
    </div>}
    {search?.status === "Cancelled" && <p role="status">{search.message ?? "Salió de la búsqueda. Puede elegir otro deck."}</p>}
    {statusError && <p className="alert error" role="alert">{statusError}</p>}
    {error && <p className="alert error" role="alert">{error}</p>}
    {(deckError || statusError) && <button type="button" disabled={loading || busy} onClick={() => {
      setLoading(true); setDeckError(""); setStatusError(""); setReload(value => value + 1);
    }}>Volver a consultar</button>}
  </section>;
}
