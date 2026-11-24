import { useMemo, useRef, useState, type ReactNode } from "react";
import { CARD_TYPES } from "../types/catalogs";
import logo from "../assets/images (3).jpg";
import { type PlayerCatalogCard } from "./playerCatalogTypes";
import { PLAYER_TEST_CARDS, STARTING_CUBE_BALANCE } from "./playerTestData";
import { usePlayerCardPage } from "../hooks/usePlayerCardPage";
import type { CardType } from "../types/catalogs";

type Stage = "login" | "welcome" | "game";
type Section = "catalog" | "collection" | "decks" | "matchmaking";

import PlayerCard from "../components/PlayerCard";
import SavedDecks from "../components/SavedDecks";
import { GAME_RULES } from "../constants/gameRules";
import { createPlayerDeck } from "../services/playerApi";
import FindMatchPage from "./FindMatchPage";

const PAGE_SIZE = GAME_RULES.catalogPageSize;
const COLLECTION_PAGE_SIZE = GAME_RULES.collectionPageSize;

interface PlayerExperiencePageProps {
  onOpenAdmin: () => void;
  initialStage?: Stage;
  onSignOut?: () => void;
  onSignIn?: () => Promise<void>;
  loginButton?: ReactNode;
  loginError?: string;
  loginStatus?: string;
  initialCollectionMessage?: string | null;
  cards?: PlayerCatalogCard[];
  remoteData?: boolean;
  cubeBalance?: number;
  playerName?: string;
}

/** Presenta las pantallas de jugador y recibe los puntos de integración de autenticación y datos. */
export default function PlayerExperiencePage({
  onOpenAdmin,
  initialStage = "login",
  onSignOut,
  onSignIn,
  loginButton,
  loginError: externalLoginError,
  loginStatus,
  initialCollectionMessage,
  cards: initialCards = PLAYER_TEST_CARDS,
  remoteData = false,
  cubeBalance = STARTING_CUBE_BALANCE,
  playerName = "Jugador",
}: PlayerExperiencePageProps) {
  const [stage, setStage] = useState<Stage>(initialStage);
  const [section, setSection] = useState<Section>("catalog");
  const [localLoginError, setLocalLoginError] = useState("");
  const [authenticating, setAuthenticating] = useState(false);
  const [onlyOwned, setOnlyOwned] = useState(false);
  const [typeFilter, setTypeFilter] = useState("");
  const [costFilter, setCostFilter] = useState("");
  const [page, setPage] = useState(0);
  const catalogPage = usePlayerCardPage(remoteData && stage === "game" && section === "catalog", false, {
    type: typeFilter ? typeFilter as CardType : undefined,
    cost: costFilter === "" ? undefined : Number(costFilter),
    ownedOnly: onlyOwned, pageSize: PAGE_SIZE,
  });
  const collectionPage = usePlayerCardPage(remoteData && (stage === "welcome" || (stage === "game" && (section === "collection" || section === "decks"))), true, { pageSize: COLLECTION_PAGE_SIZE });
  const cards = useMemo(() => remoteData
    ? [...new Map([...catalogPage.knownCards, ...collectionPage.knownCards].map(card => [card.code, card])).values()]
    : initialCards, [remoteData, initialCards, catalogPage.knownCards, collectionPage.knownCards]);
  const [deckCards, setDeckCards] = useState<string[]>([]);
  const [deckName, setDeckName] = useState("");
  const [savedDeckName, setSavedDeckName] = useState("");
  const [deckListVersion, setDeckListVersion] = useState(0);
  const [savingDeck, setSavingDeck] = useState(false);
  const [deckError, setDeckError] = useState("");
  const deckRequest = useRef<{ fingerprint: string; key: string } | null>(null);
  const deckSaveInProgress = useRef(false);
  const cardsByCode = useMemo(() => new Map(cards.map(card => [card.code, card])), [cards]);
  const ownedCards = useMemo(
    () => cards.filter((card) => card.quantity > 0),
    [cards],
  );
  const ownedCount = remoteData ? collectionPage.totalCount : ownedCards.length;
  const filteredCards = useMemo(() => cards.filter((card) => {
    const matchesType = !typeFilter || card.type === typeFilter;
    const matchesCost = costFilter === "" || card.cost === Number(costFilter);
    const matchesOwnership = !onlyOwned || card.quantity > 0;
    return matchesType && matchesCost && matchesOwnership;
  }), [cards, costFilter, onlyOwned, typeFilter]);
  const resultCount = remoteData ? catalogPage.totalCount : filteredCards.length;
  const pageCount = Math.ceil(resultCount / PAGE_SIZE);
  const visibleCards = remoteData ? catalogPage.items : filteredCards.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);
  const collectionVisible = remoteData ? collectionPage.items : ownedCards;

  function updateFilters(update: () => void) {
    catalogPage.reset();
    update();
    setPage(0);
  }

  function toggleDeckCard(code: string) {
    if (deckSaveInProgress.current) return;
    setSavedDeckName("");
    setDeckError("");
    setDeckCards((current) => current.includes(code)
      ? current.filter((cardCode) => cardCode !== code)
      : current.length < GAME_RULES.deckSize ? [...current, code] : current);
  }

  async function signIn() {
    setLocalLoginError("");
    setAuthenticating(true);
    try {
      await onSignIn?.();
      setStage("welcome");
    } catch {
      setLocalLoginError("No se pudo completar el inicio de sesión. Inténtalo de nuevo más tarde.");
    } finally {
      setAuthenticating(false);
    }
  }

  async function saveDeck() {
    const name = deckName.trim();
    if (!remoteData || deckSaveInProgress.current || deckCards.length !== GAME_RULES.deckSize || !name) return;
    const input = { name, cardCodes: [...deckCards].sort() };
    const fingerprint = JSON.stringify(input);
    if (deckRequest.current?.fingerprint !== fingerprint) {
      deckRequest.current = { fingerprint, key: crypto.randomUUID() };
    }
    deckSaveInProgress.current = true;
    setSavingDeck(true);
    setDeckError("");
    setSavedDeckName("");
    try {
      const saved = await createPlayerDeck(input, deckRequest.current.key);
      deckRequest.current = null;
      setDeckCards([]);
      setDeckName("");
      setSavedDeckName(saved.name);
      setDeckListVersion(version => version + 1);
    } catch (cause) {
      setDeckError(cause instanceof Error ? cause.message : "No se pudo guardar el mazo. Inténtalo de nuevo.");
    } finally {
      deckSaveInProgress.current = false;
      setSavingDeck(false);
    }
  }

  if (stage === "login") {
    return (
      <main className="player-login">
        <section className="login-visual">
          <img src={logo} alt="Poké-Arena" />
          <span className="eyebrow">Colecciona · Construye · Compite</span>
          <h1>Tu arena,<br />tus estrategias.</h1>
          <p>Descubre nuevas cartas, forma tu colección y prepara tu próximo mazo.</p>
          <div className="login-orbit login-orbit-one" aria-hidden="true">✦</div>
          <div className="login-orbit login-orbit-two" aria-hidden="true">ϟ</div>
          <div className="login-visual-note"><span className="arena-mark" /> Una aventura empieza con una carta.</div>
        </section>
        <section className="login-content">
          <div className="login-card">
            <span className="eyebrow">Bienvenido a Poké-Arena</span>
            <h2>Inicia sesión</h2>
            <p>Usa tu cuenta de Google para continuar con tu cuenta de jugador.</p>
            {loginButton ?? (
              <button type="button" className="provider-button" disabled={authenticating} onClick={() => void signIn()}>
                <span className="provider-symbol" aria-hidden="true">↗</span>
                {authenticating ? "Conectando..." : "Continuar con proveedor"}
              </button>
            )}
            {loginStatus && <p className="login-status" role="status">{loginStatus}</p>}
            {(externalLoginError || localLoginError) && (
              <p className="alert error" role="alert">{externalLoginError || localLoginError}</p>
            )}
            <button type="button" className="admin-entry" onClick={onOpenAdmin}>Ir al estudio administrativo</button>
          </div>
          <footer>© 2026 Poké-Arena <span>·</span> Hecho para grandes estrategias</footer>
        </section>
      </main>
    );
  }

  if (stage === "welcome") {
    return (
      <main className="player-welcome">
        <div className="welcome-topline">
          <img src={logo} alt="" />
          <span className="brand-pill">POKÉ-ARENA</span>
        </div>
        <section className="welcome-panel">
          <div className="welcome-confetti" aria-hidden="true">✦</div>
          <span className="eyebrow">¡Tu aventura comienza!</span>
          <h1>Tu cuenta está<br />lista para explorar</h1>
          <p className="welcome-copy">Ya tienes todo listo para empezar a jugar.</p>
          <div className="reward-row">
            <div className="reward-card reward-cards">
              <span className="reward-icon" aria-hidden="true">▤</span>
              <strong>{ownedCount}</strong>
              <span>{ownedCount === 1 ? "carta en colección" : "cartas en colección"}</span>
            </div>
            <div className="reward-card reward-cubes">
              <span className="reward-icon" aria-hidden="true">◈</span>
              <strong>{cubeBalance?.toLocaleString("es-ES") ?? "—"}</strong>
              <span>cubos disponibles</span>
            </div>
          </div>
          <div className="welcome-collection">
            <div className="section-heading">
              <div><h2>Mi colección</h2><p>Estas cartas ya están disponibles para tus mazos.</p></div>
              {ownedCount > 0 && <span>ACTUALIZADA</span>}
            </div>
            {remoteData && <PageStatus data={collectionPage} />}
            {ownedCards.length > 0 ? (
              <div className="mini-card-row">
                {ownedCards.slice(0, GAME_RULES.welcomePreviewCount).map((card) => <PlayerCard key={card.code} card={card} variant="mini" />)}
                {ownedCount > GAME_RULES.welcomePreviewCount && <span className="more-cards">+{ownedCount - GAME_RULES.welcomePreviewCount}</span>}
              </div>
            ) : !collectionPage.loading && !collectionPage.error && <p className="player-empty-inline">No hay cartas para mostrar.</p>}
          </div>
          <div className="welcome-actions">
            <button type="button" className="player-primary" onClick={() => {
              setStage("game");
              setSection("catalog");
            }}>Explorar el catálogo <span aria-hidden="true">→</span></button>
          </div>
        </section>
        <p className="welcome-footer">¡Que comience la partida!</p>
      </main>
    );
  }

  const sectionTitles: Record<Section, string> = {
    catalog: "Explorar catálogo",
    collection: "Mi colección",
    decks: "Constructor de mazos",
    matchmaking: "Buscar partida",
  };

  return (
    <div className="player-app">
      <aside className="player-sidebar">
        <div className="player-brand">
          <img src={logo} alt="Poké-Arena" />
          <span>POKÉ-ARENA</span>
        </div>
        <p className="nav-label">Jugar</p>
        <nav aria-label="Navegación de jugador">
          {([
            ["catalog", "Catálogo", "⌕"],
            ["collection", "Mi colección", "▤"],
            ["decks", "Mis mazos", "▧"],
            ["matchmaking", "Buscar partida", "⚔"],
          ] as [Section, string, string][]).map(([key, label, mark]) => (
            <button key={key} type="button" className={section === key ? "nav-active" : ""}
              onClick={() => setSection(key)} aria-current={section === key ? "page" : undefined}>
              <span className="player-nav-mark" aria-hidden="true">{mark}</span>
              <span>{label}</span>
              {key === "decks" && <span className="nav-count">{deckCards.length}/{GAME_RULES.deckSize}</span>}
            </button>
          ))}
        </nav>
        <div className="player-sidebar-spacer" />
        <div className="player-sidebar-collection">
          <span className="arena-mark" aria-hidden="true" />
          <strong>Tu colección</strong>
          <span>{ownedCount} cartas disponibles</span>
          <div className="collection-meter"><span style={{ width: `${Math.min(ownedCount / GAME_RULES.initialCollectionSize * 100, 100)}%` }} /></div>
        </div>
        <button type="button" className="player-admin-link" onClick={onOpenAdmin}>Estudio administrativo</button>
      </aside>

      <main className="player-main">
        {initialCollectionMessage && <p className="alert" role="status">{initialCollectionMessage}</p>}
        <header className="player-topbar">
          <div className="player-breadcrumb">Poké-Arena <span>/</span> <strong>{sectionTitles[section]}</strong></div>
          <div className="player-account">
            <span className="cube-balance"><span aria-hidden="true">◈</span> {cubeBalance?.toLocaleString("es-ES") ?? "—"}</span>
            <span className="account-avatar" aria-hidden="true">{playerName.slice(0, 1).toUpperCase()}</span>
            <span className="account-name">{playerName}</span>
            <button type="button" aria-label="Cerrar sesión" className="logout-button" onClick={() => {
              setDeckCards([]);
              setSavedDeckName("");
              if (onSignOut) onSignOut();
              else setStage("login");
            }}>Salir</button>
          </div>
        </header>

        {section === "matchmaking" && <FindMatchPage />}
        {section === "catalog" && (
          <section className="player-page">
            <header className="player-page-heading">
              <div><p className="eyebrow">Tu próxima estrategia empieza aquí</p><h1>Explora el catálogo</h1>
                <p>Descubre cartas y encuentra las combinaciones perfectas para tu mazo.</p></div>
              <div className="owned-summary"><span>▤</span><div><strong>{ownedCount}</strong><small>cartas en tu colección</small></div></div>
            </header>
            <div className="catalog-tools">
              <label>Tipo de carta
                <select value={typeFilter} onChange={(event) => updateFilters(() => setTypeFilter(event.target.value))}>
                  <option value="">Todos los tipos</option>
                  {CARD_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
                </select>
              </label>
              <label>Costo de energía
                <select value={costFilter} onChange={(event) => updateFilters(() => setCostFilter(event.target.value))}>
                  <option value="">Cualquier costo</option>
                  {Array.from({ length: GAME_RULES.maximumCost + 1 }, (_, cost) => <option key={cost} value={cost}>{cost} {cost === 1 ? "energía" : "energías"}</option>)}
                </select>
              </label>
              <label className="owned-filter">
                <input type="checkbox" checked={onlyOwned} onChange={(event) => updateFilters(() => setOnlyOwned(event.target.checked))} />
                Mostrar solo cartas propias
              </label>
              <span className="catalog-result-count">{resultCount} cartas encontradas</span>
            </div>
            {remoteData && <PageStatus data={catalogPage} />}
            <div className="player-card-grid">
              {visibleCards.map((card) => <PlayerCard key={card.code} card={card}
                quantity={card.quantity} />)}
            </div>
            {visibleCards.length === 0 && !catalogPage.loading && !catalogPage.error && <div className="empty-state"><span className="empty-icon">⌕</span>
              <h3>No encontramos cartas</h3>
              <p>Prueba con otros filtros para ver más opciones.</p>
            </div>}
            {remoteData ? <ServerPagination data={catalogPage} /> : pageCount > 1 && <div className="player-pagination">
              <span>Mostrando {page * PAGE_SIZE + 1}–{Math.min((page + 1) * PAGE_SIZE, filteredCards.length)} de {filteredCards.length}</span>
              <div>
                <button type="button" disabled={page === 0} onClick={() => setPage((current) => current - 1)}>Anterior</button>
                <strong>Página {page + 1} de {pageCount}</strong>
                <button type="button" disabled={page + 1 >= pageCount} onClick={() => setPage((current) => current + 1)}>Siguiente</button>
              </div>
            </div>}
          </section>
        )}

        {section === "collection" && (
          <section className="player-page">
            <header className="player-page-heading">
              <div><p className="eyebrow">Lo que has reunido</p><h1>Mi colección</h1>
                <p>Las cartas que posees estarán disponibles para tus mazos.</p></div>
              <div className="owned-summary"><span>▤</span><div><strong>{ownedCount}</strong><small>cartas reunidas</small></div></div>
            </header>
            <div className="collection-highlight"><span className="collection-highlight-icon">◈</span>
              <div><strong>{cubeBalance?.toLocaleString("es-ES") ?? "—"} cubos</strong><span>Saldo de la cuenta</span></div>
              <span className="collection-highlight-badge">CUENTA DE JUGADOR</span>
            </div>
            {remoteData && <PageStatus data={collectionPage} />}
            <div className="player-card-grid collection-grid">
              {collectionVisible.map((card) => <PlayerCard key={card.code} card={card} quantity={card.quantity}
                selected={deckCards.includes(card.code)} canAdd={deckCards.length < GAME_RULES.deckSize || deckCards.includes(card.code)}
                deckBusy={savingDeck} onDeckToggle={() => toggleDeckCard(card.code)} />)}
            </div>
            {remoteData && <ServerPagination data={collectionPage} />}
            {collectionVisible.length === 0 && !collectionPage.loading && !collectionPage.error && <div className="empty-state"><span className="empty-icon">▤</span><h3>Tu colección está vacía</h3><p>Explora el catálogo para encontrar cartas.</p></div>}
          </section>
        )}

        {section === "decks" && (
          <section className="player-page deck-page">
            <header className="player-page-heading">
              <div><p className="eyebrow">Prepara tu estrategia</p><h1>Construye tu mazo</h1>
                <p>Elige {GAME_RULES.deckSize} cartas de las que ya tienes en tu colección.</p></div>
              <div className="deck-count-card"><strong>{deckCards.length}<span>/{GAME_RULES.deckSize}</span></strong><small>cartas seleccionadas</small></div>
            </header>
            <div className="deck-editor panel">
              <label>Nombre del mazo<input value={deckName} disabled={savingDeck} maxLength={GAME_RULES.maximumDeckNameLength} onChange={(event) => {
                setDeckName(event.target.value);
                setSavedDeckName("");
                setDeckError("");
              }} /></label>
              <div className="deck-progress" aria-label={`${deckCards.length} de ${GAME_RULES.deckSize} cartas seleccionadas`}><span style={{ width: `${deckCards.length / GAME_RULES.deckSize * 100}%` }} /></div>
              <div className="deck-slots" aria-label="Espacios del mazo">
                {Array.from({ length: GAME_RULES.deckSize }, (_, index) => {
                  const code = deckCards[index];
                  const name = cardsByCode.get(code)?.name ?? code;
                  return <div key={index} className={`deck-slot ${code ? "is-filled" : ""}`}>
                    <span>{String(index + 1).padStart(2, "0")}</span>
                    {code ? <><strong>{name}</strong><button type="button" disabled={savingDeck} aria-label={`Quitar ${name} del mazo`} onClick={() => toggleDeckCard(code)}>×</button></> : <small>Elige una carta</small>}
                  </div>;
                })}
              </div>
              <div className="deck-actions">
                <span>{!remoteData ? "Inicia sesión para guardar tus mazos."
                  : deckCards.length < GAME_RULES.deckSize ? `Te faltan ${GAME_RULES.deckSize - deckCards.length} cartas para completar tu mazo.`
                  : "Mazo completo."}</span>
                <button type="button" className="player-primary" disabled={!remoteData || savingDeck || deckCards.length !== GAME_RULES.deckSize || !deckName.trim()}
                  onClick={() => void saveDeck()}>{savingDeck ? "Guardando…" : "Guardar mazo"}</button>
              </div>
              {savedDeckName && <p className="alert success" role="status">“{savedDeckName}” se guardó correctamente.</p>}
              {deckError && <p className="alert error" role="alert">{deckError}</p>}
            </div>
            {remoteData && <SavedDecks key={deckListVersion} knownCards={cardsByCode} />}
            <div className="section-heading deck-pick-heading"><div><h2>Cartas disponibles</h2><p>Solo puedes agregar cartas de tu colección.</p></div><span>{ownedCount} cartas propias</span></div>
            <div className="player-card-grid deck-pick-grid">
              {collectionVisible.map((card) => <PlayerCard key={card.code} card={card} quantity={card.quantity}
                selected={deckCards.includes(card.code)} canAdd={deckCards.length < GAME_RULES.deckSize || deckCards.includes(card.code)}
                deckBusy={savingDeck} onDeckToggle={() => toggleDeckCard(card.code)} />)}
            </div>
            {remoteData && <><PageStatus data={collectionPage} /><ServerPagination data={collectionPage} /></>}
            {collectionVisible.length === 0 && !collectionPage.loading && !collectionPage.error && <div className="empty-state"><span className="empty-icon">▤</span><h3>No tienes cartas disponibles</h3><p>Agrega cartas a tu colección antes de armar un mazo.</p></div>}
          </section>
        )}
      </main>
    </div>
  );
}

/** Comparte estados de red y navegación entre catálogo y colección. */
function PageStatus({ data }: { data: ReturnType<typeof usePlayerCardPage> }) {
  if (data.loading) return <p role="status">Cargando cartas...</p>;
  if (data.error) return <div className="alert error" role="alert">{data.error} <button type="button" onClick={data.retry}>Reintentar</button></div>;
  return null;
}

function ServerPagination({ data }: { data: ReturnType<typeof usePlayerCardPage> }) {
  if (data.loading || data.error || data.totalCount === 0) return null;
  return <div className="player-pagination">
    <span>{data.totalCount} cartas encontradas</span>
    <div>
      <button type="button" disabled={data.page === 0} onClick={data.previous}>Anterior</button>
      <strong>Página {data.page + 1}</strong>
      <button type="button" disabled={data.nextPosition === null} onClick={data.next}>Siguiente</button>
    </div>
  </div>;
}
