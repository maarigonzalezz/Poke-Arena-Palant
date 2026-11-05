import { useMemo, useState } from "react";
import { CARD_TYPES } from "../types/catalogs";
import logo from "../assets/images (3).jpg";
import {
  INITIAL_CARD_CODES,
  PLAYER_DEMO_CARDS,
  TYPE_MARKS,
  type PlayerDemoCard,
} from "./playerDemoData";

type Stage = "login" | "welcome" | "game";
type Section = "catalog" | "collection" | "decks";

const PAGE_SIZE = 12;

/** Interfaz de demostración del recorrido de jugador; no conecta servicios de autenticación ni catálogo. */
export default function PlayerExperiencePage({ onOpenAdmin }: { onOpenAdmin: () => void }) {
  const [stage, setStage] = useState<Stage>("login");
  const [section, setSection] = useState<Section>("catalog");
  const [showLoginError, setShowLoginError] = useState(false);
  const [showCatalogWait, setShowCatalogWait] = useState(false);
  const [onlyOwned, setOnlyOwned] = useState(false);
  const [typeFilter, setTypeFilter] = useState("");
  const [costFilter, setCostFilter] = useState("");
  const [page, setPage] = useState(0);
  const [deckCards, setDeckCards] = useState<string[]>([]);
  const [deckName, setDeckName] = useState("Mi primer mazo");
  const [deckSaved, setDeckSaved] = useState(false);

  const ownedCards = useMemo(
    () => PLAYER_DEMO_CARDS.filter((card) => INITIAL_CARD_CODES.includes(card.code)),
    [],
  );
  const filteredCards = useMemo(() => PLAYER_DEMO_CARDS.filter((card) => {
    const matchesType = !typeFilter || card.type === typeFilter;
    const matchesCost = costFilter === "" || card.cost === Number(costFilter);
    const matchesOwnership = !onlyOwned || INITIAL_CARD_CODES.includes(card.code);
    return matchesType && matchesCost && matchesOwnership;
  }), [costFilter, onlyOwned, typeFilter]);
  const pageCount = Math.ceil(filteredCards.length / PAGE_SIZE);
  const visibleCards = filteredCards.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);

  function updateFilters(update: () => void) {
    update();
    setPage(0);
  }

  function toggleDeckCard(code: string) {
    setDeckSaved(false);
    setDeckCards((current) => current.includes(code)
      ? current.filter((cardCode) => cardCode !== code)
      : current.length < 12 ? [...current, code] : current);
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
            <p>Usa tu proveedor de identidad para continuar con tu cuenta de jugador.</p>
            <button type="button" className="provider-button" onClick={() => {
              setShowLoginError(false);
              setStage("welcome");
            }}>
              <span className="provider-symbol" aria-hidden="true">↗</span>
              Continuar con proveedor
            </button>
            <div className="login-separator"><span>o</span></div>
            <button type="button" className="login-secondary" onClick={() => setShowLoginError(true)}>
              Ver ejemplo de proveedor no disponible
            </button>
            {showLoginError && (
              <p className="alert error" role="alert">
                No se pudo contactar al proveedor de identidad. Inténtalo de nuevo más tarde.
              </p>
            )}
            <p className="prototype-note"><strong>Prototipo visual:</strong> el botón simula el ingreso; no valida una identidad real.</p>
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
          <span className="prototype-pill">DEMO DE INTERFAZ</span>
        </div>
        <section className="welcome-panel">
          <div className="welcome-confetti" aria-hidden="true">✦</div>
          <span className="eyebrow">¡Tu aventura comienza!</span>
          <h1>{showCatalogWait ? <>Aún no podemos<br />entregarla</> : <>Tu colección inicial<br />ya está lista</>}</h1>
          {showCatalogWait ? (
            <div className="alert error welcome-error" role="alert">
              <strong>Aún no podemos preparar tu colección.</strong>
              <span>El catálogo necesita al menos 15 cartas comunes. No se han entregado cartas ni cubos; inténtalo en otro momento.</span>
              <button type="button" className="text-button" onClick={() => setShowCatalogWait(false)}>Volver a la entrega de muestra</button>
            </div>
          ) : (
            <>
              <p className="welcome-copy">Recibiste cartas distintas para formar tu primer mazo y empezar a explorar.</p>
              <div className="reward-row">
                <div className="reward-card reward-cards">
                  <span className="reward-icon" aria-hidden="true">▤</span>
                  <strong>15</strong>
                  <span>cartas comunes únicas</span>
                </div>
                <div className="reward-card reward-cubes">
                  <span className="reward-icon" aria-hidden="true">◈</span>
                  <strong>1,000</strong>
                  <span>cubos iniciales</span>
                </div>
              </div>
              <div className="welcome-collection">
                <div className="section-heading">
                  <div><h2>Tu colección inicial</h2><p>Una carta de cada especie · 15 en total</p></div>
                  <span>LISTA PARA JUGAR</span>
                </div>
                <div className="mini-card-row">
                  {ownedCards.slice(0, 8).map((card) => <MiniCard key={card.code} card={card} />)}
                  <span className="more-cards">+7</span>
                </div>
              </div>
              <div className="welcome-actions">
                <button type="button" className="player-primary" onClick={() => {
                  setStage("game");
                  setSection("catalog");
                }}>Explorar el catálogo <span aria-hidden="true">→</span></button>
                <button type="button" className="player-text-button" onClick={() => setShowCatalogWait(true)}>Ver ejemplo de catálogo incompleto</button>
              </div>
            </>
          )}
          {showCatalogWait && <button type="button" className="player-primary" onClick={() => setShowCatalogWait(false)}>Entendido</button>}
        </section>
        <p className="welcome-footer">Esta entrega se muestra como ejemplo; no se guarda en una cuenta real.</p>
      </main>
    );
  }

  const sectionTitles: Record<Section, string> = {
    catalog: "Explorar catálogo",
    collection: "Mi colección",
    decks: "Constructor de mazos",
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
          ] as [Section, string, string][]).map(([key, label, mark]) => (
            <button key={key} type="button" className={section === key ? "nav-active" : ""}
              onClick={() => setSection(key)} aria-current={section === key ? "page" : undefined}>
              <span className="player-nav-mark" aria-hidden="true">{mark}</span>
              <span>{label}</span>
              {key === "decks" && <span className="nav-count">{deckCards.length}/12</span>}
            </button>
          ))}
        </nav>
        <div className="player-sidebar-spacer" />
        <div className="player-sidebar-collection">
          <span className="arena-mark" aria-hidden="true" />
          <strong>Tu colección</strong>
          <span>15 cartas disponibles</span>
          <div className="collection-meter"><span /></div>
        </div>
        <button type="button" className="player-admin-link" onClick={onOpenAdmin}>Estudio administrativo</button>
      </aside>

      <main className="player-main">
        <header className="player-topbar">
          <div className="player-breadcrumb">Poké-Arena <span>/</span> <strong>{sectionTitles[section]}</strong></div>
          <div className="player-account">
            <span className="cube-balance"><span aria-hidden="true">◈</span> 1,000</span>
            <span className="account-avatar" aria-hidden="true">J</span>
            <span className="account-name">Jugador demo</span>
            <button type="button" aria-label="Cerrar sesión" className="logout-button" onClick={() => {
              setStage("login");
              setDeckCards([]);
              setDeckSaved(false);
            }}>Salir</button>
          </div>
        </header>

        <div className="prototype-banner" role="note"><strong>Prototipo visual</strong> · Datos de muestra; filtros y mazo funcionan solo en esta sesión del navegador.</div>

        {section === "catalog" && (
          <section className="player-page">
            <header className="player-page-heading">
              <div><p className="eyebrow">Tu próxima estrategia empieza aquí</p><h1>Explora el catálogo</h1>
                <p>Descubre cartas y encuentra las combinaciones perfectas para tu mazo.</p></div>
              <div className="owned-summary"><span>▤</span><div><strong>15</strong><small>cartas en tu colección</small></div></div>
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
                  {Array.from({ length: 11 }, (_, cost) => <option key={cost} value={cost}>{cost} {cost === 1 ? "energía" : "energías"}</option>)}
                </select>
              </label>
              <label className="owned-filter">
                <input type="checkbox" checked={onlyOwned} onChange={(event) => updateFilters(() => setOnlyOwned(event.target.checked))} />
                Mostrar solo cartas propias
              </label>
              <span className="catalog-result-count">{filteredCards.length} cartas encontradas</span>
            </div>
            <div className="player-card-grid">
              {visibleCards.map((card) => <PlayerCard key={card.code} card={card}
                quantity={INITIAL_CARD_CODES.includes(card.code) ? 1 : 0}
                selected={deckCards.includes(card.code)}
                canAdd={deckCards.length < 12 || deckCards.includes(card.code)}
                onDeckToggle={() => toggleDeckCard(card.code)} />)}
            </div>
            {visibleCards.length === 0 && <div className="empty-state"><span className="empty-icon">⌕</span><h3>No encontramos cartas</h3><p>Prueba con otros filtros para ver más opciones.</p></div>}
            {pageCount > 1 && <div className="player-pagination">
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
                <p>15 cartas únicas para descubrir y llevar a tus mazos.</p></div>
              <div className="owned-summary"><span>▤</span><div><strong>15 <small>/ 24</small></strong><small>cartas reunidas</small></div></div>
            </header>
            <div className="collection-highlight"><span className="collection-highlight-icon">◈</span>
              <div><strong>1,000 cubos</strong><span>Tu saldo inicial de demostración</span></div>
              <span className="collection-highlight-badge">CUENTA DE JUGADOR</span>
            </div>
            <div className="player-card-grid collection-grid">
              {ownedCards.map((card) => <PlayerCard key={card.code} card={card} quantity={1}
                selected={deckCards.includes(card.code)} canAdd={deckCards.length < 12 || deckCards.includes(card.code)}
                onDeckToggle={() => toggleDeckCard(card.code)} />)}
            </div>
          </section>
        )}

        {section === "decks" && (
          <section className="player-page deck-page">
            <header className="player-page-heading">
              <div><p className="eyebrow">Prepara tu estrategia</p><h1>Construye tu mazo</h1>
                <p>Elige 12 cartas de las que ya tienes en tu colección.</p></div>
              <div className="deck-count-card"><strong>{deckCards.length}<span>/12</span></strong><small>cartas seleccionadas</small></div>
            </header>
            <div className="deck-editor panel">
              <label>Nombre del mazo<input value={deckName} maxLength={40} onChange={(event) => {
                setDeckName(event.target.value);
                setDeckSaved(false);
              }} /></label>
              <div className="deck-progress" aria-label={`${deckCards.length} de 12 cartas seleccionadas`}><span style={{ width: `${deckCards.length / 12 * 100}%` }} /></div>
              <div className="deck-slots" aria-label="Espacios del mazo">
                {Array.from({ length: 12 }, (_, index) => {
                  const card = ownedCards.find((owned) => owned.code === deckCards[index]);
                  return <div key={index} className={`deck-slot ${card ? "is-filled" : ""}`}>
                    <span>{String(index + 1).padStart(2, "0")}</span>
                    {card ? <><strong>{card.name}</strong><button type="button" aria-label={`Quitar ${card.name} del mazo`} onClick={() => toggleDeckCard(card.code)}>×</button></> : <small>Elige una carta</small>}
                  </div>;
                })}
              </div>
              <div className="deck-actions">
                <span>{deckCards.length < 12 ? `Te faltan ${12 - deckCards.length} cartas para completar tu mazo.` : "¡Mazo completo! Ya puedes guardarlo."}</span>
                <button type="button" className="player-primary" disabled={deckCards.length !== 12 || !deckName.trim()}
                  onClick={() => setDeckSaved(true)}>Guardar mazo</button>
              </div>
              {deckSaved && <p className="alert success" role="status">“{deckName}” está listo. (Se conserva solo durante esta demostración.)</p>}
            </div>
            <div className="section-heading deck-pick-heading"><div><h2>Cartas disponibles</h2><p>Solo puedes agregar cartas de tu colección.</p></div><span>{ownedCards.length} cartas propias</span></div>
            <div className="player-card-grid deck-pick-grid">
              {ownedCards.map((card) => <PlayerCard key={card.code} card={card} quantity={1}
                selected={deckCards.includes(card.code)} canAdd={deckCards.length < 12 || deckCards.includes(card.code)}
                onDeckToggle={() => toggleDeckCard(card.code)} />)}
            </div>
          </section>
        )}
      </main>
    </div>
  );
}

function MiniCard({ card }: { card: PlayerDemoCard }) {
  return <div className={`mini-card type-${card.type.toLowerCase()}`} title={card.name}>
    <span>{TYPE_MARKS[card.type]}</span>
  </div>;
}

function PlayerCard({ card, quantity, selected, canAdd, onDeckToggle }: {
  card: PlayerDemoCard;
  quantity: number;
  selected: boolean;
  canAdd: boolean;
  onDeckToggle: () => void;
}) {
  return (
    <article className={`player-card type-${card.type.toLowerCase()}`} data-type={card.type}>
      <div className="player-card-art">
        <span className="player-card-code">{card.code}</span>
        <span className="player-card-symbol" aria-hidden="true">{TYPE_MARKS[card.type]}</span>
        <span className="player-card-art-name">{card.name}</span>
        <span className="player-card-type">{card.type}</span>
      </div>
      <div className="player-card-content">
        <div className="player-card-title"><h3>{card.name}</h3><span className={`rarity-tag rarity-${card.rarity.toLowerCase().replace(" ", "-")}`}>{card.rarity}</span></div>
        <p className="player-card-ability">{card.ability}</p>
        <div className="player-card-stats"><span><small>COSTO</small><strong>◈ {card.cost}</strong></span><span><small>PODER</small><strong>✦ {card.power}</strong></span></div>
        <div className={`owned-status ${quantity ? "is-owned" : ""}`}>
          <span aria-hidden="true">{quantity ? "✓" : "○"}</span>
          {quantity ? `En tu colección · ${quantity} copia` : "Aún no la tienes"}
        </div>
        <button type="button" className={`deck-toggle ${selected ? "is-selected" : ""}`}
          disabled={!quantity || (!selected && !canAdd)} onClick={onDeckToggle}>
          {selected ? "Quitar del mazo" : canAdd ? "＋ Agregar al mazo" : "Mazo completo"}
        </button>
      </div>
    </article>
  );
}
