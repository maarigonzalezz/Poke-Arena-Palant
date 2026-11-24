import { useEffect, useId, useRef, useState } from "react";
import { TYPE_MARKS } from "../pages/playerCatalogTypes";
import { getPlayerDecks, type DeckCardPreview, type PlayerDeck } from "../services/playerApi";
import CardImage from "./CardImage";
import Icon from "./Icon";

type DeckListState =
  | { status: "loading" }
  | { status: "ready"; decks: PlayerDeck[] }
  | { status: "error"; message: string };

const dateFormat = new Intl.DateTimeFormat("es-CR", { day: "numeric", month: "short", year: "numeric" });

export default function SavedDecks({ knownCards }: { knownCards: ReadonlyMap<string, DeckCardPreview> }) {
  const [state, setState] = useState<DeckListState>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const detailsId = useId();
  const detailHeading = useRef<HTMLHeadingElement>(null);
  const activeTile = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const controller = new AbortController();
    getPlayerDecks(controller.signal).then(decks => {
      if (!controller.signal.aborted) setState({ status: "ready", decks });
    }).catch((cause: unknown) => {
      if (!controller.signal.aborted) {
        const message = cause instanceof Error ? cause.message : "No se pudieron consultar tus mazos.";
        setState({ status: "error", message });
      }
    });
    return () => controller.abort();
  }, [attempt]);

  const decks = state.status === "ready" ? state.decks : [];
  const selected = decks.find(deck => deck.id === selectedId);

  useEffect(() => {
    if (selected) detailHeading.current?.focus();
  }, [selected]);

  function deckCards(deck: PlayerDeck) {
    const previews = new Map(deck.cards?.map(card => [card.code, card]));
    return deck.cardCodes.map(code => ({ code, card: previews.get(code) ?? knownCards.get(code) }));
  }

  return <section className="saved-decks" aria-labelledby="saved-decks-title">
    <header className="saved-decks-heading">
      <h2 id="saved-decks-title">Mazos guardados</h2>
      {decks.length > 0 && <span>{decks.length} {decks.length === 1 ? "mazo" : "mazos"}</span>}
    </header>
    {state.status === "loading" && <p className="saved-decks-status" role="status">Consultando tus mazos…</p>}
    {state.status === "error" && <div className="saved-decks-status">
      <p className="alert error" role="alert">{state.message}</p>
      <button type="button" className="bot-match-toggle" onClick={() => {
        setState({ status: "loading" });
        setAttempt(current => current + 1);
      }}>Reintentar</button>
    </div>}
    {state.status === "ready" && (decks.length === 0
      ? <div className="saved-decks-empty">
          <span className="saved-decks-empty-icon"><Icon name="card" /></span>
          <div><strong>Todavía no tienes mazos guardados</strong><p>Guarda tu primera selección de 12 cartas para verla aquí.</p></div>
        </div>
      : <div className="saved-deck-gallery">
          {decks.map(deck => {
            const previews = deckCards(deck);
            const withArt = previews.filter(({ card }) => card?.imageSmallUrl || card?.imageUrl);
            const cover = [...withArt, ...previews.filter(preview => !withArt.includes(preview))].slice(0, 3);
            const expanded = selectedId === deck.id;
            return <button key={deck.id} type="button" className="saved-deck-tile" ref={expanded ? activeTile : undefined}
              aria-expanded={expanded} aria-controls={detailsId}
              onClick={() => setSelectedId(current => current === deck.id ? null : deck.id)}>
              <span className="saved-deck-cover">
                <span className="saved-deck-stack" aria-hidden="true">
                  {Array.from({ length: 3 }, (_, index) => <DeckArtwork key={index} card={cover[index]?.card} />)}
                </span>
                <span className="saved-deck-size"><Icon name="card" />{deck.cardCodes.length} cartas</span>
              </span>
              <span className="saved-deck-summary">
                <span className="saved-deck-name">{deck.name}</span>
                <time dateTime={deck.createdAt}>{dateFormat.format(new Date(deck.createdAt))}</time>
                <span className="saved-deck-action">{expanded ? "Ocultar cartas" : "Ver cartas"} <Icon name="arrow" /></span>
              </span>
            </button>;
          })}
        </div>)}
    <div id={detailsId} hidden={!selected}>
      {selected && <div className="saved-deck-details">
        <header>
          <h3 ref={detailHeading} tabIndex={-1}>{selected.name}<span>{selected.cardCodes.length} cartas</span></h3>
          <button type="button" className="saved-deck-close" onClick={() => {
            activeTile.current?.focus();
            setSelectedId(null);
          }}>Cerrar</button>
        </header>
        <ul>
          {deckCards(selected).map(({ code, card }) => <li key={code} className="saved-deck-entry">
            <DeckArtwork card={card} />
            <div>
              <strong>{card?.name ?? code}</strong>
              {card && <span>{card.type} · Costo {card.cost} · Poder {card.power}</span>}
            </div>
          </li>)}
        </ul>
      </div>}
    </div>
  </section>;
}

function DeckArtwork({ card }: { card?: DeckCardPreview }) {
  return <span className="saved-deck-art" data-type={card?.type} aria-hidden="true">
    <span className="saved-deck-mark">{card ? TYPE_MARKS[card.type] : "✦"}</span>
    {(card?.imageSmallUrl || card?.imageUrl) && <CardImage card={card} small={!!card.imageSmallUrl} />}
  </span>;
}
